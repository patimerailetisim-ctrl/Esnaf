using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Business
{
    /// <summary>
    /// Müşteri üretimi ve müşteri değeri (GDD v0.2 6.2, 10.4, 11.2; v0.3 P4). Sahip: Business (GDD v0.3 3.2).
    ///
    /// Her sabah 5 (üst sınır) müşteri yuvası SABİT sırayla çekilir (her yuva için 4 <c>NextDouble</c>: kişi, değer hatası, güven, ürün seçimi).
    /// Kaç yuvanın dükkâna GELDİĞİ raftaki ürün sayısına bağlıdır (<c>2 + ⌈raf × 0,5⌉</c>, üst sınır 5) ve gün içinde en yüksek raf doluluğuna
    /// göre artar, azalmaz. Gelen müşteri, etiketli ürünler arasından kendine uygun olanla ilgilenir; hiçbiri uygun değilse gider ("kaçan müşteri").
    /// Bir müşterinin ilgisi çekimlerinden ve raf durumundan TÜRETİLİR (sorgular durum değiştirmez).
    /// </summary>
    public sealed class CustomerService : INewDayHook
    {
        private const string Stream = "customers";

        private readonly ContentDatabase _content;
        private readonly CustomerState _state;
        private readonly IdGenerator _ids;
        private readonly InstanceStore _store;
        private readonly InventoryState _inventory;
        private readonly NpcStateStore _npcs;
        private readonly DemandModel _demand;
        private readonly TimeState _time;
        private readonly RngStreams _rng;
        private readonly ValueCalculator _calculator;

        public CustomerState State
        {
            get { return _state; }
        }

        public CustomerService(
            ContentDatabase content,
            CustomerState state,
            IdGenerator ids,
            InstanceStore store,
            InventoryState inventory,
            NpcStateStore npcs,
            DemandModel demand,
            TimeState time,
            RngStreams rng)
        {
            if (content == null || state == null || ids == null || store == null || inventory == null
                || npcs == null || demand == null || time == null || rng == null)
            {
                throw new ArgumentNullException(nameof(content), "CustomerService needs all of its collaborators.");
            }

            _content = content;
            _state = state;
            _ids = ids;
            _store = store;
            _inventory = inventory;
            _npcs = npcs;
            _demand = demand;
            _time = time;
            _rng = rng;
            _calculator = new ValueCalculator(content.ValueTables);
        }

        /// <summary>Yeni günün müşteri havuzunu çeker ve o günün gelenlerini raftan belirler.</summary>
        public void OnDayOpened(int day)
        {
            CustomerConstants constants = _content.Customers;
            var candidates = new List<NpcDefinition>();
            foreach (NpcDefinition npc in _content.Npcs)
            {
                if (npc.Customer.AvailableFromDay <= day)
                {
                    candidates.Add(npc);
                }
            }

            IRandom rng = _rng.Get(Stream);
            var slots = new List<CustomerSlot>(constants.CountMax);
            int rich = 0;
            for (int i = 0; i < constants.CountMax; i++)
            {
                double npcDraw = rng.NextDouble();
                double valueDraw = rng.NextDouble();
                double trustDraw = rng.NextDouble();
                double pickDraw = rng.NextDouble();

                var pool = new List<NpcDefinition>(candidates.Count);
                foreach (NpcDefinition npc in candidates)
                {
                    if (rich < constants.RichMaxPerDay || !constants.IsRich(npc.Id))
                    {
                        pool.Add(npc);
                    }
                }

                NpcDefinition chosen = pool[Math.Min(pool.Count - 1, (int)(npcDraw * pool.Count))];
                if (constants.IsRich(chosen.Id))
                {
                    rich++;
                }

                slots.Add(new CustomerSlot(_ids.Next(), chosen.Id, valueDraw, trustDraw, pickDraw));
            }

            _state.Replace(slots);
            _state.Arrived = constants.ArrivalCount(_inventory.Count);
            _state.QueueCursor = 0; // günlük müşteri kuyruğu yeni günle baştan başlar (Gün 12.2)
            _state.QueueSkipped = 0;
        }

        /// <summary>Rafta en az bir SATILABİLİR (etiketli) ürün var mı? Yoksa müşteri gelmez (Gün 12.6).</summary>
        public bool HasSellableStock()
        {
            foreach (long id in _inventory.ItemIds)
            {
                if (_store.Get(id).ListPrice.IsPositive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Raf arttıysa gelen müşteri sayısını yükseltir (alıştan sonra çağrılır); asla düşürmez.</summary>
        public void RefreshArrivals()
        {
            int count = _content.Customers.ArrivalCount(_inventory.Count);
            if (count > _state.Arrived)
            {
                _state.Arrived = count;
            }
        }

        /// <summary>Gün sonu adım 1: satın almadan giden (gelmiş ama satılmamış) müşteri sayısını kaydeder.</summary>
        public int EndDay()
        {
            int missed = 0;
            int arrived = Math.Min(_state.Arrived, _state.Slots.Count);
            for (int i = 0; i < arrived; i++)
            {
                if (_state.Slots[i].Status != CustomerStatus.Sold)
                {
                    missed++;
                }
            }

            _state.MissedTotal += missed;
            return missed;
        }

        /// <summary>Dükkâna gelmiş, hâlâ bekleyen ve ilgilendiği bir ürün olan müşteriler ve ürünleri (geliş sırasıyla).</summary>
        public IReadOnlyList<KeyValuePair<CustomerSlot, long>> Interested()
        {
            var list = new List<KeyValuePair<CustomerSlot, long>>();
            int arrived = Math.Min(_state.Arrived, _state.Slots.Count);
            for (int i = 0; i < arrived; i++)
            {
                CustomerSlot slot = _state.Slots[i];
                if (slot.Status != CustomerStatus.Waiting)
                {
                    continue;
                }

                long instanceId = FindInterest(slot);
                if (instanceId != 0)
                {
                    list.Add(new KeyValuePair<CustomerSlot, long>(slot, instanceId));
                }
            }

            return list;
        }

        /// <summary>Gelmiş ve bekleyen yuvayı bulur.</summary>
        public bool TryGetWaiting(long customerId, out CustomerSlot slot)
        {
            int arrived = Math.Min(_state.Arrived, _state.Slots.Count);
            for (int i = 0; i < arrived; i++)
            {
                if (_state.Slots[i].CustomerId == customerId && _state.Slots[i].Status == CustomerStatus.Waiting)
                {
                    slot = _state.Slots[i];
                    return true;
                }
            }

            slot = null;
            return false;
        }

        /// <summary>Müşterinin ilgilendiği ürün (kimlik); yoksa 0. Uygun ürünler arasından seçim çekimiyle seçilir.</summary>
        public long FindInterest(CustomerSlot slot)
        {
            var eligible = new List<long>();
            foreach (long id in _inventory.ItemIds)
            {
                ProductInstance instance = _store.Get(id);
                double max;
                if (IsEligible(slot, instance, out max))
                {
                    eligible.Add(id);
                }
            }

            if (eligible.Count == 0)
            {
                return 0;
            }

            return eligible[Math.Min(eligible.Count - 1, (int)(slot.PickDraw * eligible.Count))];
        }

        /// <summary>
        /// Raftaki ürün için "müşterilerin gelebileceği" en yüksek etiket fiyatı (yalnızca BİLGİ; hiçbir karar buna bağlı değildir): o gün gelebilecek müşteri NPC'lerinden
        /// ürünün segmentini kabul eden ve ürünü oyuncuya satmamış olanların her biri için mevcut <see cref="MaxFor"/> TARAFSIZ çekimle (u = 0,5: σ etkisi sıfır) çağrılır ve
        /// <see cref="IsEligible"/>'ın kullandığı tavan (SellTooExpensiveRatio × Max) alınır; sonuç bu tavanların EN KÜÇÜĞÜdür (10 ₺'ye aşağı yuvarlı), yani uygun her NPC tipi
        /// bu fiyatta ilgi duyar. Uygun NPC yoksa <see cref="Money.Zero"/>. σ &gt; 0 olan NPC'nin gerçek çekimi bunun altına düşebilir; bu bir garanti değil, güvenli bir üst sınırdır.
        /// Yeni ekonomi formülü yoktur; ekonomi, Max, kişilikler ve IsEligible değişmez.
        /// </summary>
        public Money DemandCeilingFor(ProductInstance instance)
        {
            ProductDefinition definition = _content.GetProduct(instance.DefinitionId);
            double ceiling = double.MaxValue;
            bool any = false;
            foreach (NpcDefinition npc in _content.Npcs)
            {
                if (npc.Customer.AvailableFromDay > _time.Day
                    || !npc.Customer.AcceptsSegment(definition.Segment)
                    || _npcs.HasSoldToPlayer(npc.Id, instance.InstanceId))
                {
                    continue;
                }

                var neutral = new CustomerSlot(0L, npc.Id, 0.5, 0.5, 0.5);
                double limit = _content.Negotiation.SellTooExpensiveRatio * MaxFor(neutral, instance, false);
                ceiling = Math.Min(ceiling, limit);
                any = true;
            }

            if (!any)
            {
                return Money.Zero;
            }

            long tl = (long)Math.Floor(ceiling / 10.0) * 10L;
            return tl > 0 ? Money.FromTl(tl) : Money.Zero;
        }

        /// <summary>
        /// Ürün bu müşteriye uygun mu: etiketli, segment uyumlu, aynı NPC'nin oyuncuya sattığı ürün değil (anti-arbitraj) ve
        /// etiket ≤ "çok pahalı" oranı × M. <paramref name="max"/> müşterinin bu ürün için Max'ıdır.
        /// </summary>
        public bool IsEligible(CustomerSlot slot, ProductInstance instance, out double max)
        {
            max = 0.0;
            if (!instance.ListPrice.IsPositive)
            {
                return false;
            }

            NpcCustomerRole customer = _content.GetNpc(slot.NpcId).Customer;
            ProductDefinition definition = _content.GetProduct(instance.DefinitionId);
            if (!customer.AcceptsSegment(definition.Segment))
            {
                return false;
            }

            if (_npcs.HasSoldToPlayer(slot.NpcId, instance.InstanceId))
            {
                return false;
            }

            max = MaxFor(slot, instance, false);
            return instance.ListPrice.Tl <= _content.Negotiation.SellTooExpensiveRatio * max;
        }

        /// <summary>
        /// Müşterinin bu ürün için Max'ı (M): <c>V_müşteri × mRatio × paket × (1 + dükkân primi)</c>, üst sınır gerçek değer × 1,25, 10 TL'ye yuvarlı.
        /// <c>V_müşteri = gerçek değer (güncel talep dahil) × (1 + σ × (2u − 1))</c>; rapor gösterildiyse σ küçülür.
        /// </summary>
        public double MaxFor(CustomerSlot slot, ProductInstance instance, bool reportShown)
        {
            NpcCustomerRole customer = _content.GetNpc(slot.NpcId).Customer;
            ProductDefinition definition = _content.GetProduct(instance.DefinitionId);
            CustomerConstants constants = _content.Customers;

            double trueValue = _calculator.TrueValue(instance, definition, _demand.Multiplier(definition.Id, _time.Day)).Tl;
            double sigma = customer.ValueSigma * (reportShown ? _content.Negotiation.SellReportSigmaFactor : 1.0);
            double seen = trueValue * (1.0 + sigma * (2.0 * slot.ValueDraw - 1.0));
            bool packaged = instance.GetFlag(Esnaf.Domain.Phone.PhoneAttributes.Box) || instance.GetFlag(Esnaf.Domain.Phone.PhoneAttributes.Invoice);
            double max = seen * customer.ValueRatio * (packaged ? customer.PackageRatio : 1.0) * (1.0 + constants.ShopPremium);
            max = Math.Min(max, trueValue * constants.MaxRatioToTrueValue);
            return Math.Floor(max / 10.0 + 0.5) * 10.0;
        }

        /// <summary>Müşterinin açılış teklifi: M × açılış oranı, 10 TL'ye yuvarlı (GDD Senaryo 2: 0,80 × 9.150 = 7.320).</summary>
        public Money OpeningFor(string npcId, double max)
        {
            double opening = _content.GetNpc(npcId).Customer.OpeningOfferRatio * max;
            return Money.FromTl((long)Math.Floor(opening / 10.0 + 0.5) * 10L);
        }

        /// <summary>Bu müşteri ve ürün için satış pazarlığının başlangıç durumu (güven 50 ± 10, aciliyet NPC'nin kendi değeri).</summary>
        public SaleSetup BuildSetup(CustomerSlot slot, ProductInstance instance)
        {
            NpcDefinition npc = _content.GetNpc(slot.NpcId);
            NegotiationRules rules = _content.Negotiation;
            double max = MaxFor(slot, instance, false);
            int trust = StartTrustOf(slot);
            return new SaleSetup(OpeningFor(slot.NpcId, max), max, npc.Customer.Patience, trust, npc.Seller.Urgency, _time.Day);
        }

        /// <summary>
        /// Müşterinin istenen fiyatı pazarlıksız kabul edebileceği tavan oranı (M'nin oranı): mevcut kişilik sayılarından ve o anki güvenden türer
        /// (<see cref="DirectAcceptPolicy"/>); rastgelelik ve yeni durum yoktur.
        /// </summary>
        public double DirectAcceptRatioOf(string npcId, int trust)
        {
            NpcDefinition npc = _content.GetNpc(npcId);
            return DirectAcceptPolicy.Ratio(
                npc.Customer.OpeningOfferRatio, npc.Customer.ValueRatio, npc.Customer.Patience, npc.Seller.Urgency, npc.Customer.ValueSigma, trust);
        }

        /// <summary>Yuvanın başlangıç güveni: 50 ± 10 (çekime göre), 0–100'e kırpılır. Satış kurulumu ve kişilik profili aynı sayıyı kullanır.</summary>
        private int StartTrustOf(CustomerSlot slot)
        {
            NegotiationRules rules = _content.Negotiation;
            int trust = rules.StartTrust
                + (int)Math.Round(rules.StartTrustSpread * (2.0 * slot.TrustDraw - 1.0), MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(100, trust));
        }

        /// <summary>NPC'nin kişilik profili (yuva bilgisi olmadan); kişilik kataloğu yoksa null. Salt okunur, rastgelelik kullanmaz.</summary>
        public CustomerProfile ProfileOf(string npcId)
        {
            return CustomerProfiler.Build(_content.GetNpc(npcId), _content.Personalities, _content.Negotiation);
        }

        /// <summary>Yuvanın profili: NPC profili + bu yuvanın başlangıç güven düzeyi (mevcut çekimden, yeni çekim yok).</summary>
        public CustomerProfile ProfileFor(CustomerSlot slot)
        {
            CustomerProfile profile = ProfileOf(slot.NpcId);
            return profile == null ? null : profile.WithStartTrust(NegotiationLevels.MoodOf(_content.Negotiation, StartTrustOf(slot)));
        }

        internal void MarkSold(CustomerSlot slot)
        {
            slot.Status = CustomerStatus.Sold;
        }

        internal void MarkLeft(CustomerSlot slot)
        {
            slot.Status = CustomerStatus.Left;
        }
    }
}
