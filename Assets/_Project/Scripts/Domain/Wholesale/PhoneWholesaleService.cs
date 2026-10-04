using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Phone;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>
    /// Telefon toptancısı (Gün 14): <c>ProductDefinition → WholesaleOffer → paket → envanter</c>. VERİ ODAKLIDIR: hangi telefonların satıldığı, birim maliyet, paket adedi ve açılış günü
    /// wholesale.json'daki ürün tekliflerinden gelir; belirli bir modele özel kod YOKTUR (yeni telefon = yeni <c>ProductDefinition</c> + yeni teklif satırı). Aksesuar toptancısıyla aynı
    /// <see cref="WholesaleCatalog"/>, aynı teklif türü, aynı defter türü (wholesale_purchase) ve aynı hata kodlarını paylaşır.
    ///
    /// Paket ATOMİKTİR ve bölünemez: miktar her zaman teklifin packSize'ıdır; toplam = unitCost × packSize. Paket alınınca packSize adet AYRI <see cref="ProductInstance"/> mevcut
    /// <see cref="InventoryService"/>'e girer (aynı tanım, sıfır kondisyon: pil/kasa %100, orijinal ekran, sağlam kamera, kutu + fatura, yaş 0; maliyet tabanı = birim maliyet; etiket fiyatı 0 →
    /// oyuncu Raf'tan fiyatlar). Tüm doğrulamalar (toptancı, ürün, teklif, gün, tutar, nakit, raf kapasitesi) hiçbir durum değişmeden ÖNCE yapılır; başarısızlıkta nakit, defter ve envanter
    /// değişmez. RASTGELELİK, pazarlık ve fiyat dalgalanması YOKTUR.
    /// Hatalar: supplier.unknown, product.unknown, offer.unknown, wholesale.not_available_yet, amount.invalid, cash.insufficient, inventory.full.
    /// </summary>
    public sealed class PhoneWholesaleService
    {
        /// <summary>Telefon sektörünün kimliği (phone_models.json "sector"); yalnızca bu sektördeki ürünler telefon toptancısından alınır.</summary>
        public const string PhoneSector = "phone";

        private readonly ContentDatabase _content;
        private readonly WholesaleCatalog _wholesale;
        private readonly InventoryService _inventory;
        private readonly EconomyService _economy;
        private readonly IdGenerator _instanceIds;

        public PhoneWholesaleService(ContentDatabase content, InventoryService inventory, EconomyService economy, IdGenerator instanceIds)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            if (instanceIds == null)
            {
                throw new ArgumentNullException(nameof(instanceIds));
            }

            _content = content;
            _wholesale = content.Wholesale;
            _inventory = inventory;
            _economy = economy;
            _instanceIds = instanceIds;
        }

        /// <summary>Verilen günde alınabilen telefon teklifleri (içerikten otomatik keşfedilir).</summary>
        public IReadOnlyList<WholesaleOffer> OffersOn(int day)
        {
            return _wholesale.ProductOffersAvailableOn(day);
        }

        /// <summary>Toptancının bir telefon modelinin paketini alır. Miktar teklifin packSize'ıdır.</summary>
        public Result<PhonePackReceipt> BuyPack(string supplierId, string productId, int day)
        {
            bool supplierKnown = false;
            foreach (WholesaleOffer candidate in _wholesale.ProductOffers)
            {
                if (candidate.SupplierId == supplierId)
                {
                    supplierKnown = true;
                    break;
                }
            }

            if (!supplierKnown)
            {
                return Fail("supplier.unknown", "Unknown supplier '" + supplierId + "'.");
            }

            ProductDefinition definition;
            if (productId == null || !_content.TryGetProduct(productId, out definition) || definition.Sector != PhoneSector)
            {
                return Fail("product.unknown", "Unknown phone model '" + productId + "'.");
            }

            WholesaleOffer offer;
            if (!_wholesale.TryGetProduct(supplierId, productId, out offer))
            {
                return Fail("offer.unknown", "Supplier '" + supplierId + "' has no offer for '" + productId + "'.");
            }

            if (day < offer.AvailableFromDay)
            {
                return Fail("wholesale.not_available_yet", "The offer opens on day " + offer.AvailableFromDay + " (today is day " + day + ").");
            }

            long totalTl;
            try
            {
                totalTl = checked(offer.UnitCost.Tl * offer.PackSize);
            }
            catch (OverflowException)
            {
                return Fail("amount.invalid", "The pack cost is too large.");
            }

            Money total = Money.FromTl(totalTl);
            if (_economy.Cash < total)
            {
                return Fail("cash.insufficient", "Cash " + _economy.Cash + " is not enough for " + total + ".");
            }

            ProductInstance template = NewUnitOf(definition, supplierId);

            // Kapasiteyi ÖNCEDEN doğrula (Receive de doğrular; burada başarısızlık nakit/deftere dokunmadan döner).
            // Receive'in hata verip vermeyeceğini defter yazılmadan bilmek için önce boş yer sayılır.
            Result<IReadOnlyList<long>> capacity = _inventory.CanReceive(offer.PackSize);
            if (capacity.IsFailure)
            {
                return Fail(capacity.ErrorCode, capacity.Message);
            }

            Result<TransactionRecord> paid = _economy.RecordWholesalePurchase(supplierId, productId, offer.PackSize, total, day);
            if (paid.IsFailure)
            {
                return Fail(paid.ErrorCode, paid.Message);
            }

            Result<IReadOnlyList<long>> received = _inventory.Receive(template, offer.PackSize, offer.UnitCost, day, _instanceIds);
            if (received.IsFailure)
            {
                throw new InvalidOperationException("The shelf refused a pack it had already been checked for: " + received.Message);
            }

            return Result<PhonePackReceipt>.Ok(new PhonePackReceipt(supplierId, productId, offer.PackSize, offer.UnitCost, total, paid.Value.Id, received.Value));
        }

        // Sıfır (yeni) birim şablonu: yalnızca tanımın kendi verisinden (baz hafıza) ve telefon niteliklerinin "en iyi" değerlerinden kurulur.
        private static ProductInstance NewUnitOf(ProductDefinition definition, string supplierId)
        {
            var unit = new ProductInstance
            {
                DefinitionId = definition.Id,
                StorageGb = definition.BaseStorageGb,
                AgeMonths = 0,
                SellerNpcId = supplierId
            };
            unit.Attributes[PhoneAttributes.Battery] = AttributeValue.FromNumber(100);
            unit.Attributes[PhoneAttributes.Body] = AttributeValue.FromNumber(100);
            unit.Attributes[PhoneAttributes.Screen] = AttributeValue.FromText(PhoneAttributes.ScreenOriginal);
            unit.Attributes[PhoneAttributes.Camera] = AttributeValue.FromText(PhoneAttributes.CameraOk);
            unit.Attributes[PhoneAttributes.Box] = AttributeValue.FromFlag(true);
            unit.Attributes[PhoneAttributes.Invoice] = AttributeValue.FromFlag(true);
            return unit;
        }

        private static Result<PhonePackReceipt> Fail(string code, string message)
        {
            return Result<PhonePackReceipt>.Fail(code, message);
        }
    }
}
