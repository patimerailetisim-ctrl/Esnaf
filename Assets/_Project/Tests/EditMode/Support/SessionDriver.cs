using System;
using System.Collections.Generic;
using System.Linq;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// Yalnızca <see cref="IGameApi"/> üzerinden, test tarafındaki ayrı bir <see cref="Random"/> ile oyunu "rastgele ama tekrarlanabilir" oynar:
    /// aynı durum + aynı Random tohumu → aynı komutlar. Kayıt/yükleme testlerinde iki oturumu aynı komut dizisiyle sürmek için kullanılır.
    /// Başarısız komutlar yok sayılır (amaç durum çeşitliliğidir).
    /// </summary>
    public static class SessionDriver
    {
        private static readonly string[] Levels = { "s0", "s1", "s2", "s3" };

        public static void Run(GameSession s, Random rnd, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Step(s, rnd);
            }
        }

        public static void Step(GameSession s, Random rnd)
        {
            IGameApi api = s.Api;
            NegotiationView buy = api.GetNegotiation();
            SaleView sale = api.GetSale();

            if (buy != null)
            {
                StepBuy(api, buy, rnd);
                return;
            }

            if (sale != null)
            {
                StepSale(api, sale, rnd);
                return;
            }

            int pick = rnd.Next(12);
            if (pick <= 3)
            {
                IReadOnlyList<ListingView> listings = api.GetListings();
                if (listings.Count > 0)
                {
                    api.StartNegotiation(listings[rnd.Next(listings.Count)].ListingId);
                }
            }
            else if (pick == 4)
            {
                IReadOnlyList<ListingView> listings = api.GetListings();
                if (listings.Count > 0)
                {
                    api.StartAppraisal(listings[rnd.Next(listings.Count)].ListingId, Levels[rnd.Next(Levels.Length)]);
                }
            }
            else if (pick == 5 || pick == 6)
            {
                foreach (StockLine line in api.GetInventory().ToList())
                {
                    if (rnd.Next(2) == 0)
                    {
                        api.SetPrice(line.InstanceId, Money.FromTl(Money.RoundTo10((long)(line.CostBasis.Tl * (1.05 + rnd.NextDouble() * 0.2)))));
                    }
                }
            }
            else if (pick <= 9)
            {
                IReadOnlyList<CustomerView> customers = api.GetCustomers();
                if (customers.Count > 0)
                {
                    api.StartSale(customers[rnd.Next(customers.Count)].CustomerId);
                }
            }
            else
            {
                api.EndDay();
            }
        }

        private static void StepBuy(IGameApi api, NegotiationView buy, Random rnd)
        {
            int pick = rnd.Next(10);
            if (buy.Phase == NegotiationPhase.FinalOffer)
            {
                if (pick < 6)
                {
                    api.AcceptFinalPrice();
                }
                else
                {
                    api.WalkAway();
                }

                return;
            }

            if (pick == 0)
            {
                api.WalkAway();
                return;
            }

            long offer = Money.RoundTo10((long)(buy.ShownPrice.Tl * (0.70 + rnd.NextDouble() * 0.35)));
            offer = Math.Max(10, offer);
            if (pick <= 3 && buy.Cards.Count > 0)
            {
                NegotiationCardView card = buy.Cards[rnd.Next(buy.Cards.Count)];
                api.MakeOfferWithCard(Money.FromTl(offer), card.AppraisalId, card.CardIndex);
            }
            else
            {
                api.MakeOffer(Money.FromTl(offer));
            }
        }

        private static void StepSale(IGameApi api, SaleView sale, Random rnd)
        {
            int pick = rnd.Next(10);
            if (sale.Phase == NegotiationPhase.FinalOffer)
            {
                if (pick < 7)
                {
                    api.AcceptCustomerFinalOffer();
                }
                else
                {
                    api.LetCustomerGo();
                }

                return;
            }

            if (pick == 0)
            {
                api.LetCustomerGo();
                return;
            }

            if (pick == 1 && sale.Reports.Count > 0 && !sale.ReportShown)
            {
                api.ShowReport(sale.Reports[0].AppraisalId);
                return;
            }

            long ask = Money.RoundTo10((long)(sale.ShownPrice.Tl * (1.0 + rnd.NextDouble() * 0.35)));
            api.AskPrice(Money.FromTl(Math.Max(10, ask)));
        }
    }
}
