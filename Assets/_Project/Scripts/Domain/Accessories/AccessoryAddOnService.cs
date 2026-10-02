using System;
using System.Globalization;
using Esnaf.Core;
using Esnaf.Domain.Economy;

namespace Esnaf.Domain.Accessories
{
    /// <summary>
    /// Telefon satışının ardından AKSESUAR EK SATIŞI (Gün 11.3.1). Aksesuar tek başına satılamaz: her ek satış, AYNI GÜN tamamlanmış bir telefon satışının
    /// defter satırına bağlanır (satır kimliği not argümanında saklanır; ek durum ve kayıt değişikliği yoktur). Pazarlık yok: fiyat aksesuarın sabit retailPrice'ıdır.
    /// Uyumluluk yok (her aksesuar her telefon satışına eklenebilir). RASTGELELİK YOKTUR. Mevcut <see cref="AccessoryStock"/> kullanılır: 1 birim çıkar ve
    /// stoktan çıkan GERÇEK (orantılı) maliyet kâr hesabına girer.
    ///
    /// ATOMİKTİR: bağlı satış, aksesuar ve stok doğrulanır; stoktan çıkarma ve defter yazma birlikte uygulanır, defter reddederse stok birebir geri konur.
    /// Hatalar: sale.unknown, addon.sale_closed, accessory.unknown, stock.insufficient, amount.invalid (+ defterin kendi hataları, örn. ledger.day_regression).
    /// </summary>
    public sealed class AccessoryAddOnService
    {
        private readonly AccessoryCatalog _catalog;
        private readonly AccessoryStock _stock;
        private readonly EconomyService _economy;
        private readonly EconomyState _state;

        public AccessoryAddOnService(AccessoryCatalog catalog, AccessoryStock stock, EconomyService economy, EconomyState state)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (stock == null)
            {
                throw new ArgumentNullException(nameof(stock));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _catalog = catalog;
            _stock = stock;
            _economy = economy;
            _state = state;
        }

        /// <summary>Bugün tamamlanan telefon satışına (<paramref name="phoneSaleRecordId"/>) bir adet aksesuar satar.</summary>
        public Result<AccessorySaleReceipt> SellAddOn(long phoneSaleRecordId, string accessoryId, int day)
        {
            // 1) bağlı telefon satışı: var olmalı, telefon satışı olmalı, aynı gün olmalı
            TransactionRecord phoneSale;
            if (!_state.Ledger.TryGetById(phoneSaleRecordId, out phoneSale) || phoneSale.TypeId != TransactionTypeIds.Sale)
            {
                return Fail("sale.unknown", "Ledger row " + phoneSaleRecordId.ToString(CultureInfo.InvariantCulture) + " is not a completed phone sale.");
            }

            if (phoneSale.Day != day)
            {
                return Fail("addon.sale_closed", "The phone sale was on day " + phoneSale.Day + "; add-ons can only be sold on the day of the sale (today is day " + day + ").");
            }

            // 2) aksesuar ve stok
            AccessoryDefinition definition;
            if (!_catalog.TryGet(accessoryId, out definition))
            {
                return Fail("accessory.unknown", "Unknown accessory '" + accessoryId + "'.");
            }

            if (_stock.Quantity(accessoryId) < 1)
            {
                return Fail("stock.insufficient", "There is no '" + accessoryId + "' in stock.");
            }

            // 3) uygula: önce stoktan 1 birim (gerçek maliyet), sonra defter; defter reddederse stok birebir geri konur
            Result<Money> removed = _stock.Remove(accessoryId, 1);
            if (removed.IsFailure)
            {
                return Fail(removed.ErrorCode, removed.Message);
            }

            Money cost = removed.Value;
            Money price = definition.RetailPrice;
            Result<TransactionRecord> booked = _economy.RecordAccessorySale(accessoryId, phoneSale.NpcId, price, cost, phoneSaleRecordId, day);
            if (booked.IsFailure)
            {
                Result restored = _stock.Add(accessoryId, 1, cost); // Remove'un tam tersi: adet +1, maliyet +cost; yer az önce açıldığı için başarısız olamaz
                if (restored.IsFailure)
                {
                    throw new InvalidOperationException("The accessory stock could not take back a unit it had just released: " + restored.Message);
                }

                return Fail(booked.ErrorCode, booked.Message);
            }

            Money phoneProfit = phoneSale.Amount - phoneSale.SaleCostBasis.Value;
            return Result<AccessorySaleReceipt>.Ok(new AccessorySaleReceipt(accessoryId, phoneSaleRecordId, booked.Value.Id, price, cost, phoneProfit));
        }

        /// <summary>
        /// Verilen günün SON tamamlanmış telefon satışı (defter satırı); o gün telefon satışı yoksa null. Ek satış fırsatı bu satıra bağlanır.
        /// Ek durum yoktur: defterden okunur (kayıtla birlikte zaten korunur).
        /// </summary>
        public TransactionRecord LatestPhoneSaleOn(int day)
        {
            TransactionRecord latest = null;
            foreach (TransactionRecord record in _state.Ledger.Records)
            {
                if (record.TypeId == TransactionTypeIds.Sale && record.Day == day)
                {
                    latest = record;
                }
            }

            return latest;
        }

        /// <summary>Bir telefon satışına bağlı aksesuar ek satışı satırları (defter sırasıyla).</summary>
        public System.Collections.Generic.IReadOnlyList<TransactionRecord> AddOnsOf(long phoneSaleRecordId)
        {
            string link = phoneSaleRecordId.ToString(CultureInfo.InvariantCulture);
            var list = new System.Collections.Generic.List<TransactionRecord>();
            foreach (TransactionRecord record in _state.Ledger.Records)
            {
                if (record.TypeId == TransactionTypeIds.AccessorySale && record.MemoArgs.Count == 1 && record.MemoArgs[0] == link)
                {
                    list.Add(record);
                }
            }

            return list;
        }

        /// <summary>
        /// Bir telefon satışının TOPLAM kârı: telefon kârı + ona bağlı tüm aksesuar ek satışlarının kârı (defterden hesaplanır; ek durum yok).
        /// Telefon satışı değilse "sale.unknown".
        /// </summary>
        public Result<Money> TotalProfitOfSale(long phoneSaleRecordId)
        {
            TransactionRecord phoneSale;
            if (!_state.Ledger.TryGetById(phoneSaleRecordId, out phoneSale) || phoneSale.TypeId != TransactionTypeIds.Sale)
            {
                return Result<Money>.Fail("sale.unknown", "Ledger row " + phoneSaleRecordId.ToString(CultureInfo.InvariantCulture) + " is not a completed phone sale.");
            }

            Money total = phoneSale.Amount - phoneSale.SaleCostBasis.Value;
            foreach (TransactionRecord record in AddOnsOf(phoneSaleRecordId))
            {
                total += record.Amount - record.SaleCostBasis.Value;
            }

            return Result<Money>.Ok(total);
        }

        private static Result<AccessorySaleReceipt> Fail(string code, string message)
        {
            return Result<AccessorySaleReceipt>.Fail(code, message);
        }
    }
}
