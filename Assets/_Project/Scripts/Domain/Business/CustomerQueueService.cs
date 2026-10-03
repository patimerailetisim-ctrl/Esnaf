using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Business
{
    /// <summary>
    /// Günlük müşteri kuyruğu (Gün 12.2, 12.6): müşteriler gün içinde, geliş saatleri geldikçe KENDİLİĞİNDEN mağazaya girer; aynı anda yalnızca 1 müşteri işlemdedir
    /// (aktif), yeni gelenler sırada bekler ve en çok <see cref="QueuePolicy.MaxWaitMinutes"/> oyun dakikası sonra satış yapmadan çıkar.
    ///
    /// <b>Plan</b> (bugünün 8–12 müşterisi, geliş saatleri, NPC'leri) SAF ve DETERMİNİSTİKTİR: ana tohum + gün numarasından türetilir (<see cref="RngStreams.Derive"/>; kayıtlı
    /// akışlara dokunmaz) ve mevcut 10 NPC/kişilik havuzundan seçilir. Geliş saati planın kendisidir; "giriş zamanı" ayrıca saklanmaz.
    ///
    /// <b>Kalıcı değerler</b> (mevcut <see cref="CustomerState"/>): <see cref="CustomerState.QueueCursor"/> (bu sıraya kadar herkes işlendi/geçti) ve
    /// <see cref="CustomerState.QueueSkipped"/> (geliş anında rafta satılabilir ürün olmadığı için hiç gelmeyenler). Bekleme ve çıkış plan + saat + imleçten TÜRETİLİR.
    ///
    /// <b>Mağazada olan müşteri</b>: imleç ve atlananlar hariç, geliş saati geldiyse; süren satıştaki müşteri hep mağazadadır; diğerleri geliş + 60 dk dolunca ya da mağaza
    /// kapanınca (21:00) çıkar. İlk sıradaki mağazada olan müşteri aktiftir. Saat ilerledikçe (<see cref="StoreClock.Advanced"/>) gelişler (rafta ürün yoksa müşteri gelmez)
    /// ve çıkışlar işlenir ve <see cref="CustomerArrived"/> / <see cref="CustomerLeftWaiting"/> olayları yayınlanır. Zaman maliyetleri (12.4) değişmez; mevcut yuva/satış sistemi
    /// (<see cref="CustomerService"/>) bu serviste değişmez.
    /// </summary>
    public sealed class CustomerQueueService
    {
        private readonly ContentDatabase _content;
        private readonly CustomerState _state;
        private readonly CustomerService _customers;
        private readonly TimeState _time;
        private readonly RngStreams _rng;
        private readonly StoreClock _clock;
        private readonly TradeState _trade;
        private readonly IEventBus _events;

        public CustomerQueueService(
            ContentDatabase content, CustomerState state, CustomerService customers, TimeState time, RngStreams rng, StoreClock clock, TradeState trade, IEventBus events)
        {
            if (content == null || state == null || customers == null || time == null || rng == null || clock == null || trade == null)
            {
                throw new ArgumentNullException(nameof(content), "CustomerQueueService needs all of its collaborators.");
            }

            _content = content;
            _state = state;
            _customers = customers;
            _time = time;
            _rng = rng;
            _clock = clock;
            _trade = trade;
            _events = events;
            _clock.Advanced += OnClockAdvanced;
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
                // +1: saat açılışta (09:00) henüz hiç ilerlemediğinden ilk müşteri en erken 09:01'de gelir (Gün 12.6: gelişler saat ilerledikçe işlenir).
                int jitter = (int)(rng.NextDouble() * (width / 2));
                jitter = jitter / QueuePolicy.ArrivalStepMinutes * QueuePolicy.ArrivalStepMinutes;
                int arrival = StoreHours.OpenMinute + 1 + i * width + jitter;

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

                plan.Add(new QueuedCustomer(i, QueueCustomerId.For(day, i), chosen.Id, arrival, _customers.ProfileOf(chosen.Id)));
            }

            return plan;
        }

        // ---------- türetilmiş durum ----------

        private bool IsSkipped(int index)
        {
            return (_state.QueueSkipped & (1 << index)) != 0;
        }

        private bool InSale(QueuedCustomer customer)
        {
            return _trade.CurrentSale != null && _trade.CurrentSale.CustomerId == customer.CustomerId;
        }

        // Müşteri şu an mağazada mı (gelmiş, çıkmamış, işlenmemiş)?
        private bool IsPresent(QueuedCustomer customer, int cursor, int minute)
        {
            if (customer.Index < cursor || IsSkipped(customer.Index) || customer.ArrivalMinute > minute)
            {
                return false;
            }

            if (InSale(customer))
            {
                return true; // süren satış 21:00'de de, 60 dk'dan sonra da tamamlanır
            }

            return minute < customer.ArrivalMinute + QueuePolicy.MaxWaitMinutes && minute < StoreHours.CloseMinute;
        }

        private int CursorOf(IReadOnlyList<QueuedCustomer> plan)
        {
            return Math.Min(_state.QueueCursor, plan.Count);
        }

        // Aktif müşteri: süren satıştaki müşteri, yoksa mağazada olanların ilki.
        private QueuedCustomer CurrentOf(IReadOnlyList<QueuedCustomer> plan, int cursor, int minute)
        {
            foreach (QueuedCustomer candidate in plan)
            {
                if (IsPresent(candidate, cursor, minute) && InSale(candidate))
                {
                    return candidate;
                }
            }

            foreach (QueuedCustomer candidate in plan)
            {
                if (IsPresent(candidate, cursor, minute))
                {
                    return candidate;
                }
            }

            return null;
        }

        // ---------- saat ilerledikçe: gelişler ve çıkışlar ----------

        private void OnClockAdvanced(int day, int from, int to)
        {
            if (day != _time.Day)
            {
                return;
            }

            IReadOnlyList<QueuedCustomer> plan = PlanFor(day);
            int cursor = CursorOf(plan);

            // 1) gelişler: geliş saati bu aralıktaysa müşteri gelir; rafta satılabilir ürün yoksa HİÇ gelmez.
            foreach (QueuedCustomer customer in plan)
            {
                if (customer.Index < cursor || IsSkipped(customer.Index) || customer.ArrivalMinute <= from || customer.ArrivalMinute > to)
                {
                    continue;
                }

                if (!_customers.HasSellableStock())
                {
                    _state.QueueSkipped |= 1 << customer.Index;
                }
                else if (_events != null)
                {
                    _events.Publish(new CustomerArrived(day, customer.CustomerId, customer.NpcId));
                }
            }

            // 2) çıkışlar: bu aralıkta mağazada olup artık olmayanlar (süre doldu ya da mağaza kapandı); süren satıştaki müşteri çıkmaz.
            foreach (QueuedCustomer customer in plan)
            {
                if (customer.Index < cursor || IsSkipped(customer.Index) || InSale(customer) || customer.ArrivalMinute > to)
                {
                    continue;
                }

                int deadline = customer.ArrivalMinute + QueuePolicy.MaxWaitMinutes;
                bool wasHere = customer.ArrivalMinute > from || (from < deadline && from < StoreHours.CloseMinute);
                bool hereNow = to < deadline && to < StoreHours.CloseMinute;
                if (wasHere && !hereNow && _events != null)
                {
                    QueueLeaveReason reason = deadline <= to ? QueueLeaveReason.Timeout : QueueLeaveReason.StoreClosed;
                    _events.Publish(new CustomerLeftWaiting(day, customer.CustomerId, customer.NpcId, reason));
                }
            }
        }

        // ---------- görünüm ve işlemler ----------

        /// <summary>Bugünün kuyruğunun görünümü, mağaza saatine göre (durumu değiştirmez).</summary>
        public CustomerQueueView GetView()
        {
            return GetView(_clock.View);
        }

        /// <summary>Bugünün kuyruğunun görünümü (durumu değiştirmez).</summary>
        public CustomerQueueView GetView(ClockView clock)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            IReadOnlyList<QueuedCustomer> plan = PlanFor(_time.Day);
            int cursor = CursorOf(plan);
            int minute = clock.MinuteOfDay;
            QueuedCustomer current = CurrentOf(plan, cursor, minute);

            var entries = new List<QueueEntryView>(plan.Count);
            var line = new List<QueuedCustomer>();
            int? next = null;
            foreach (QueuedCustomer customer in plan)
            {
                QueueStatus status;
                if (customer.Index < cursor)
                {
                    status = QueueStatus.Done;
                }
                else if (IsSkipped(customer.Index))
                {
                    status = QueueStatus.Left;
                }
                else if (customer.ArrivalMinute > minute)
                {
                    status = QueueStatus.Upcoming;
                    if (!next.HasValue && clock.IsOpen)
                    {
                        next = customer.ArrivalMinute;
                    }
                }
                else if (current != null && customer.Index == current.Index)
                {
                    status = QueueStatus.Active;
                }
                else if (IsPresent(customer, cursor, minute))
                {
                    status = QueueStatus.Waiting;
                    line.Add(customer);
                }
                else
                {
                    status = QueueStatus.Left; // süresi doldu ya da mağaza kapandı
                }

                entries.Add(new QueueEntryView(customer, status));
            }

            return new CustomerQueueView(_time.Day, plan.Count, cursor, current, line.Count, clock.IsOpen, next, entries, line);
        }

        /// <summary>
        /// Aktif müşteriyi tamamlar ve sıradakine geçer (mağaza kapalıysa kalanlar gönderilir). Hata: queue.no_active_customer (şu an aktif müşteri yok).
        /// </summary>
        public Result<CustomerQueueView> CompleteCurrent()
        {
            return CompleteCurrent(_clock.View);
        }

        /// <summary>
        /// Aktif müşteriyi tamamlar ve bu aksiyonun süresini (<paramref name="minutes"/>) mağaza saatinden harcar (Gün 12.4). Süre YALNIZCA tamamlama başarılıysa harcanır;
        /// kapanış kuralı (mağaza kapalıysa kalanlar gönderilir) süre harcandıktan sonraki saate göre uygulanır. Görünüm güncel saatle döner.
        /// </summary>
        public Result<CustomerQueueView> CompleteCurrent(int minutes)
        {
            IReadOnlyList<QueuedCustomer> plan = PlanFor(_time.Day);
            QueuedCustomer current = CurrentOf(plan, CursorOf(plan), _clock.View.MinuteOfDay);
            if (current == null)
            {
                return Result<CustomerQueueView>.Fail("queue.no_active_customer", "There is no active customer to complete.");
            }

            _clock.Spend(minutes);
            return Finish(plan, current.Index);
        }

        /// <summary><see cref="CompleteCurrent()"/>, verilen saat görünümüne göre.</summary>
        public Result<CustomerQueueView> CompleteCurrent(ClockView clock)
        {
            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            IReadOnlyList<QueuedCustomer> plan = PlanFor(_time.Day);
            QueuedCustomer current = CurrentOf(plan, CursorOf(plan), clock.MinuteOfDay);
            if (current == null)
            {
                return Result<CustomerQueueView>.Fail("queue.no_active_customer", "There is no active customer to complete.");
            }

            return Finish(plan, current.Index);
        }

        // İmleci tamamlanan müşterinin ardına taşır (süresi dolup geçilenler de atlanır); mağaza kapalıysa kalan herkes gönderilir.
        private Result<CustomerQueueView> Finish(IReadOnlyList<QueuedCustomer> plan, int index)
        {
            int cursor = Math.Max(_state.QueueCursor, index + 1);
            ClockView clock = _clock.View;
            if (!clock.IsOpen)
            {
                cursor = plan.Count; // mağaza kapalı: kuyrukta bekleyenler gider, yeni müşteri çağrılmaz
            }

            _state.QueueCursor = cursor;
            return Result<CustomerQueueView>.Ok(GetView(clock));
        }

        /// <summary>Kuyruk müşterisinin satışı bitti (anlaşma ya da ayrıldı): müşteriyi tamamlar ve sıradakine geçer (kapalıysa kalanlar gönderilir).</summary>
        internal void CompleteForSale(long customerId)
        {
            QueuedCustomer customer;
            CustomerSlot slot;
            if (!TryResolve(customerId, out customer, out slot) || customer.Index < _state.QueueCursor)
            {
                throw new InvalidOperationException("The customer " + customerId + " is not the active queue customer.");
            }

            Finish(PlanFor(_time.Day), customer.Index);
        }

        // ---------- mevcut müşteri/satış sistemiyle bağ (Gün 12.3) ----------

        /// <summary>
        /// Müşterinin mevcut müşteri sistemindeki (satış/pazarlık) yuvası: gün + sıra bu günün kuyruğuna aitse, müşterinin çekimleri (değer hatası, güven, ürün seçimi)
        /// ana tohumdan ve günden türetilen İKİNCİ bir kayıtsız akıştan gelir ("customer_queue_draws:gün"). Plan ve kayıtlı akışlar değişmez; yuva SAKLANMAZ.
        /// Satış, Max, güven, kişilik ve ürün seçimi bu yuva üzerinden mevcut <see cref="CustomerService"/> yöntemleriyle çalışır.
        /// </summary>
        public bool TryResolve(long customerId, out QueuedCustomer customer, out CustomerSlot slot)
        {
            customer = null;
            slot = null;
            int day;
            int index;
            if (!QueueCustomerId.TryDecode(customerId, out day, out index) || day != _time.Day)
            {
                return false;
            }

            IReadOnlyList<QueuedCustomer> plan = PlanFor(day);
            if (index >= plan.Count)
            {
                return false;
            }

            IRandom draws = _rng.Derive("customer_queue_draws:" + day.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (int i = 0; i <= index; i++)
            {
                double valueDraw = draws.NextDouble();
                double trustDraw = draws.NextDouble();
                double pickDraw = draws.NextDouble();
                if (i == index)
                {
                    customer = plan[i];
                    slot = new CustomerSlot(plan[i].CustomerId, plan[i].NpcId, valueDraw, trustDraw, pickDraw);
                }
            }

            return true;
        }

        /// <summary>
        /// Şu an aktif kuyruk müşterisi ve yuvası: süren satıştaki müşteri, yoksa mağazada olanların ilki (geliş saati geldi, 60 dk dolmadı, mağaza AÇIK); yoksa false.
        /// Mağaza kapanınca yeni müşteri başlatılmaz (kapanıştan önce başlamış bir satış sürer).
        /// </summary>
        public bool TryGetActive(out QueuedCustomer customer, out CustomerSlot slot)
        {
            customer = null;
            slot = null;
            ClockView clock = _clock.View;
            IReadOnlyList<QueuedCustomer> plan = PlanFor(_time.Day);
            QueuedCustomer current = CurrentOf(plan, CursorOf(plan), clock.MinuteOfDay);
            if (current == null || (!clock.IsOpen && !InSale(current)))
            {
                return false;
            }

            return TryResolve(current.CustomerId, out customer, out slot);
        }
    }
}
