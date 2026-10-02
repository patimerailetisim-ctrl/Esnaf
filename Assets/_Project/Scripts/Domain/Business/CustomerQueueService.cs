using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Business
{
    /// <summary>
    /// Günlük müşteri kuyruğu (Gün 12.2): mağaza açıkken müşteriler gün içinde SIRAYLA gelir, aynı anda yalnızca 1 aktif müşteri vardır.
    ///
    /// <b>Plan</b> (bugünün 8–12 müşterisi, geliş saatleri, NPC'leri) SAF ve DETERMİNİSTİKTİR: ana tohum + gün numarasından türetilir
    /// (<see cref="RngStreams.Derive"/>; kayıtlı akışlara dokunmaz) ve mevcut 10 NPC/kişilik havuzundan seçilir (zenginlik sınırı dahil). Aynı tohum + gün = aynı kuyruk;
    /// kayıtta plan YOKTUR. Tek kalıcı sayı, mevcut <see cref="CustomerState"/>'teki <see cref="CustomerState.QueueCursor"/>'dır (kaç müşteri tamamlandı).
    ///
    /// <b>Aktif müşteri</b> = imleçteki müşteri, geliş saati geldiyse (<see cref="StoreClock"/> saatine göre). <see cref="CompleteCurrent"/> imleci ilerletir; mağaza o anda
    /// kapalıysa kuyruktaki kalan müşteriler gönderilir (kapalıyken yeni müşteri çağrılmaz). Geliş saatleri hep açılış–kapanış arasındadır. İşlemlerin kaç dakika sürdüğü
    /// (zaman maliyeti) bu adımda yoktur; müşteri/satış akışıyla sonraki adımlarda bağlanır. Mevcut yuva/satış sistemi (<see cref="CustomerService"/>) bu serviste değişmez.
    /// </summary>
    public sealed class CustomerQueueService
    {
        private readonly ContentDatabase _content;
        private readonly CustomerState _state;
        private readonly CustomerService _customers;
        private readonly TimeState _time;
        private readonly RngStreams _rng;

        public CustomerQueueService(ContentDatabase content, CustomerState state, CustomerService customers, TimeState time, RngStreams rng)
        {
            if (content == null || state == null || customers == null || time == null || rng == null)
            {
                throw new ArgumentNullException(nameof(content), "CustomerQueueService needs all of its collaborators.");
            }

            _content = content;
            _state = state;
            _customers = customers;
            _time = time;
            _rng = rng;
        }

        /// <summary>Verilen günün müşteri kuyruğu planı (saf; durumdan bağımsız). Havuzda müşteri yoksa boş.</summary>
        public IReadOnlyList<QueuedCustomer> PlanFor(int day)
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

            var plan = new List<QueuedCustomer>();
            if (candidates.Count == 0)
            {
                return plan;
            }

            IRandom rng = _rng.Derive("customer_queue:" + day.ToString(System.Globalization.CultureInfo.InvariantCulture));
            int span = QueuePolicy.MaxCustomersPerDay - QueuePolicy.MinCustomersPerDay + 1;
            int count = Math.Min(QueuePolicy.MaxCustomersPerDay, QueuePolicy.MinCustomersPerDay + (int)(rng.NextDouble() * span));
            int width = StoreHours.OpenMinutes / count;
            int rich = 0;
            for (int i = 0; i < count; i++)
            {
                // Geliş saati: günün i. diliminin başı + dilimin yarısına kadar sapma (5 dk adımı); dilimler çakışmaz, sıra hep artar, kapanıştan önce biter.
                int jitter = (int)(rng.NextDouble() * (width / 2));
                jitter = jitter / QueuePolicy.ArrivalStepMinutes * QueuePolicy.ArrivalStepMinutes;
                int arrival = StoreHours.OpenMinute + i * width + jitter;

                double npcDraw = rng.NextDouble();
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

                plan.Add(new QueuedCustomer(i, chosen.Id, arrival, _customers.ProfileOf(chosen.Id)));
            }

            return plan;
        }

        /// <summary>Bugünün kuyruğunun görünümü (durumu değiştirmez).</summary>
        public CustomerQueueView GetView(ClockView clock)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            IReadOnlyList<QueuedCustomer> plan = PlanFor(_time.Day);
            int cursor = Math.Min(_state.QueueCursor, plan.Count);
            QueuedCustomer current = CurrentOf(plan, cursor, clock);

            var entries = new List<QueueEntryView>(plan.Count);
            for (int i = 0; i < plan.Count; i++)
            {
                QueueStatus status = i < cursor ? QueueStatus.Done : (current != null && i == cursor ? QueueStatus.Active : QueueStatus.Waiting);
                entries.Add(new QueueEntryView(plan[i], status));
            }

            int waiting = plan.Count - cursor - (current != null ? 1 : 0);
            int? next = current == null && cursor < plan.Count ? plan[cursor].ArrivalMinute : (int?)null;
            return new CustomerQueueView(_time.Day, plan.Count, cursor, current, waiting, clock.IsOpen, next, entries);
        }

        /// <summary>
        /// Aktif müşteriyi tamamlar ve sıradakine geçer (sıradakinin geliş saati geldiyse o aktif olur). Mağaza o anda kapalıysa kalan müşteriler gönderilir.
        /// Hata: queue.no_active_customer (şu an aktif müşteri yok).
        /// </summary>
        public Result<CustomerQueueView> CompleteCurrent(ClockView clock)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            IReadOnlyList<QueuedCustomer> plan = PlanFor(_time.Day);
            int cursor = Math.Min(_state.QueueCursor, plan.Count);
            if (CurrentOf(plan, cursor, clock) == null)
            {
                return Result<CustomerQueueView>.Fail("queue.no_active_customer", "There is no active customer to complete.");
            }

            cursor++;
            if (!clock.IsOpen)
            {
                cursor = plan.Count; // mağaza kapalı: kuyrukta bekleyenler gider, yeni müşteri çağrılmaz
            }

            _state.QueueCursor = cursor;
            return Result<CustomerQueueView>.Ok(GetView(clock));
        }

        // Aktif müşteri: imleçteki müşteri, geliş saati geldiyse.
        private static QueuedCustomer CurrentOf(IReadOnlyList<QueuedCustomer> plan, int cursor, ClockView clock)
        {
            if (cursor >= plan.Count)
            {
                return null;
            }

            QueuedCustomer candidate = plan[cursor];
            return candidate.ArrivalMinute <= clock.MinuteOfDay ? candidate : null;
        }
    }
}
