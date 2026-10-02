using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using NUnit.Framework;

namespace Esnaf.Tests.Support
{
    /// <summary>Anlaşmayla bitmiş gerçek bir müşteri telefon satışı ve müşterinin aksesuar talebi (Gün 11.3.4 testleri için).</summary>
    public sealed class AddOnDeal
    {
        public GameSession Session;
        public CustomerView Customer;
        public SaleView Deal;
        public long SaleRecordId;
        public IReadOnlyList<string> Requested;
        public ulong Seed;
        public int Bumps;
    }

    /// <summary>
    /// Gerçek müşteri akışıyla (StartSale → AskPrice 10 ₺ → anlaşma) istenen aksesuar talebine sahip bir satış bulur. Talep, satışın değişmez verisinden
    /// deterministik türediği için (rastgelelik yok) belirli bir talep için tohum aranır; bulunan tohum önbelleğe alınır.
    /// </summary>
    public static class AddOnDeals
    {
        public static readonly Money Ask = Money.FromTl(10);
        private static readonly Dictionary<string, ulong> Cache = new Dictionary<string, ulong>();

        /// <summary>Talebi <paramref name="wanted"/> koşulunu sağlayan ilk satışı döndürür.</summary>
        public static AddOnDeal Find(string key, Func<IReadOnlyList<string>, bool> wanted)
        {
            ulong cached;
            if (Cache.TryGetValue(key, out cached))
            {
                AddOnDeal known = Try(cached / 1000UL, (int)(cached % 1000UL));
                Assert.IsNotNull(known);
                return known;
            }

            // Talep; alıcı NPC, gün, defter satırı kimliği ve satış tutarından türer. Taze oturumda kimlik hep aynı olduğundan çeşitlilik için satıştan önce
            // 'bumps' kadar küçük yatırım satırı yazılır (kimliği kaydırır; stok/telefon etkilemez).
            for (int bumps = 0; bumps <= 80; bumps++)
            {
                for (ulong seed = 1; seed <= 30; seed++)
                {
                    AddOnDeal deal = Try(seed, bumps);
                    if (deal != null && wanted(deal.Requested))
                    {
                        Cache[key] = seed * 1000UL + (ulong)bumps;
                        return deal;
                    }
                }
            }

            throw new InvalidOperationException("No seed gives a customer sale whose accessory request matches '" + key + "'.");
        }

        /// <summary>Talebi <paramref name="needed"/> aksesuarların hepsini içeren bir satış.</summary>
        public static AddOnDeal Requesting(params string[] needed)
        {
            return Find("needs:" + string.Join(",", needed), r => needed.All(r.Contains));
        }

        /// <summary>Müşterinin hiç aksesuar istemediği bir satış.</summary>
        public static AddOnDeal NoRequest()
        {
            return Find("none", r => r.Count == 0);
        }

        /// <summary>Tam olarak <paramref name="count"/> aksesuar istenen bir satış.</summary>
        public static AddOnDeal WithCount(int count)
        {
            return Find("count:" + count, r => r.Count == count);
        }

        /// <summary>Yeni oturum: tohum + satıştan önce 'bumps' küçük yatırım satırı (defter satırı kimliğini kaydırır).</summary>
        public static GameSession Prepare(ulong seed, int bumps)
        {
            GameSession s = GameSession.NewGame(MarketHarness.RealContent(), seed);
            for (int i = 0; i < bumps; i++)
            {
                Assert.IsTrue(s.EconomyService.RecordInvestment(Money.FromTl(10), s.Time.Day).IsSuccess);
            }

            return s;
        }

        /// <summary>Verilen tohum/kaydırmadaki müşteri satışını yapar; müşteri/anlaşma yoksa null.</summary>
        public static AddOnDeal Try(ulong seed, int bumps = 0)
        {
            GameSession s = Prepare(seed, bumps);
            MarketListing guided = s.Market.Listings.Single(l => l.IsGuided);
            if (!s.Api.StartNegotiation(guided.ListingId).IsSuccess
                || !s.Api.MakeOffer(Money.FromTl(5800)).IsSuccess
                || !s.Api.SetPrice(guided.InstanceId, Money.FromTl(5900)).IsSuccess)
            {
                return null;
            }

            CustomerView customer = s.Api.GetCustomers().FirstOrDefault();
            if (customer == null || !s.Api.StartSale(customer.CustomerId).IsSuccess)
            {
                return null;
            }

            Result<SaleView> deal = s.Api.AskPrice(Ask);
            if (deal.IsFailure || deal.Value.Phase != NegotiationPhase.Deal)
            {
                return null;
            }

            long recordId = s.EconomyState.Ledger.Records.Last(r => r.TypeId == TransactionTypeIds.Sale).Id;
            return new AddOnDeal
            {
                Session = s,
                Customer = customer,
                Deal = deal.Value,
                SaleRecordId = recordId,
                Requested = s.AccessoryAddOns.RequestedAccessories(recordId),
                Seed = seed,
                Bumps = bumps
            };
        }
    }
}
