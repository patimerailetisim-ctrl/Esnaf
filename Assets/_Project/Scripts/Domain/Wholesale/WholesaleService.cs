using System;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Economy;

namespace Esnaf.Domain.Wholesale
{
    /// <summary>
    /// Toptancı satın alma sistemi: oyuncu → <see cref="WholesaleCatalog"/> → paket alışı → <see cref="AccessoryStock"/>. RASTGELELİK YOKTUR.
    /// Bir alış her zaman bir PAKETTİR (miktar = teklifin packSize'ı), toplam maliyet = unitCost × packSize. Para ve defter <see cref="EconomyService"/>
    /// üzerinden (tür wholesale_purchase), stok <see cref="AccessoryStock"/>'a yazılır.
    ///
    /// ATOMİKTİR: tüm doğrulamalar (teklif, gün, nakit, kapasite) hiçbir durum değiştirilmeden ÖNCE yapılır; başarısızlıkta nakit, defter ve stok değişmez.
    /// Doğrulamalar geçtikten sonra tek defter satırı yazılır ve stok eklenir; stok eklemesi kapasite önceden doğrulandığı için başarısız olamaz.
    /// Birden çok toptancıyı destekler (teklifler toptancı kimliğiyle aranır).
    /// Hatalar: supplier.unknown, accessory.unknown, offer.unknown, wholesale.not_available_yet, cash.insufficient, stock.full, amount.invalid.
    /// </summary>
    public sealed class WholesaleService
    {
        private readonly WholesaleCatalog _wholesale;
        private readonly AccessoryCatalog _accessories;
        private readonly AccessoryStock _stock;
        private readonly EconomyService _economy;

        public WholesaleService(WholesaleCatalog wholesale, AccessoryCatalog accessories, AccessoryStock stock, EconomyService economy)
        {
            if (wholesale == null)
            {
                throw new ArgumentNullException(nameof(wholesale));
            }

            if (accessories == null)
            {
                throw new ArgumentNullException(nameof(accessories));
            }

            if (stock == null)
            {
                throw new ArgumentNullException(nameof(stock));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            _wholesale = wholesale;
            _accessories = accessories;
            _stock = stock;
            _economy = economy;
        }

        /// <summary>Verilen günde alınabilen teklifler (availableFromDay ≤ gün).</summary>
        public System.Collections.Generic.IReadOnlyList<WholesaleOffer> OffersOn(int day)
        {
            return _wholesale.AvailableOn(day);
        }

        /// <summary>Toptancının bir aksesuar paketini alır. Miktar teklifin packSize'ıdır.</summary>
        public Result<WholesalePurchaseReceipt> BuyPack(string supplierId, string accessoryId, int day)
        {
            // 1) teklif: toptancı → aksesuar → teklif
            bool supplierKnown = false;
            foreach (WholesaleOffer candidate in _wholesale.Offers)
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

            AccessoryDefinition definition;
            if (!_accessories.TryGet(accessoryId, out definition))
            {
                return Fail("accessory.unknown", "Unknown accessory '" + accessoryId + "'.");
            }

            WholesaleOffer offer;
            if (!_wholesale.TryGet(supplierId, accessoryId, out offer))
            {
                return Fail("offer.unknown", "Supplier '" + supplierId + "' has no offer for '" + accessoryId + "'.");
            }

            // 2) gün
            if (day < offer.AvailableFromDay)
            {
                return Fail("wholesale.not_available_yet", "The offer opens on day " + offer.AvailableFromDay + " (today is day " + day + ").");
            }

            // 3) tutar: unitCost × packSize (taşma = geçersiz)
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

            // 4) nakit ve 5) kapasite: durum değişmeden önce
            if (_economy.Cash < total)
            {
                return Fail("cash.insufficient", "Cash " + _economy.Cash + " is not enough for " + total + ".");
            }

            if (offer.PackSize > _stock.FreeUnits)
            {
                return Fail("stock.full", "The accessory shelf has " + _stock.FreeUnits + " free units; the pack has " + offer.PackSize + ".");
            }

            // 6) uygula: önce defter (başarısızsa hiçbir şey değişmemiştir), sonra stok (kapasite doğrulandı)
            Result<TransactionRecord> paid = _economy.RecordWholesalePurchase(supplierId, accessoryId, offer.PackSize, total, day);
            if (paid.IsFailure)
            {
                return Fail(paid.ErrorCode, paid.Message);
            }

            Result added = _stock.Add(accessoryId, offer.PackSize, total);
            if (added.IsFailure)
            {
                throw new InvalidOperationException("The accessory stock refused a pack it had already been checked for: " + added.Message);
            }

            return Result<WholesalePurchaseReceipt>.Ok(new WholesalePurchaseReceipt(supplierId, accessoryId, offer.PackSize, offer.UnitCost, total, paid.Value.Id));
        }

        private static Result<WholesalePurchaseReceipt> Fail(string code, string message)
        {
            return Result<WholesalePurchaseReceipt>.Fail(code, message);
        }
    }
}
