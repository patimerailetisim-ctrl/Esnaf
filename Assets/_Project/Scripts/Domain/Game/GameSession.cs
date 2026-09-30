using System;
using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Business;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Game
{
    /// <summary>
    /// Oyunun "kasası" (GDD v0.3 3.2): bütün sistemleri ve durumlarını bir arada tutan TEK nesne; composition root'tur.
    /// Bağımlılıklar constructor'la verilir; global/tekil durum yoktur (K7), iki oturum tamamen bağımsızdır.
    /// UI ve simülasyon oyuna yalnızca <see cref="Api"/> üzerinden dokunur; diğer üyeler kompozisyon, test ve (Gün 9'da)
    /// kayıt katmanı içindir.
    /// </summary>
    public sealed class GameSession
    {
        public ContentDatabase Content { get; }
        public IEventBus Bus { get; }
        public TimeState Time { get; }
        public RngStreams Rng { get; }
        public IdGenerator InstanceIds { get; }
        public IdGenerator ListingIds { get; }
        public InstanceStore Store { get; }
        public MarketState Market { get; }
        public NpcStateStore Npcs { get; }
        public IdGenerator AppraisalIds { get; }
        public KnowledgeState Knowledge { get; }
        public EquipmentState Equipment { get; }
        public AppraisalService Appraisal { get; }
        public IdGenerator CustomerIds { get; }
        public CustomerService Customers { get; }
        public DemandState DemandState { get; }
        public DemandModel Demand { get; }
        public TradeState TradeState { get; }
        public TradeService Trade { get; }
        public SellService Sell { get; }
        public EconomyState EconomyState { get; }
        public EconomyService EconomyService { get; }
        public InventoryState InventoryState { get; }
        public InventoryService InventoryService { get; }
        public WealthCalculator Wealth { get; }
        public DaySummaryBuilder Summaries { get; }
        public LedgerView LedgerView { get; }
        public ListingGenerator ListingGenerator { get; }
        public DayEndPipeline DayEnd { get; }
        public IGameApi Api { get; }

        private GameSession(ContentDatabase content, ulong seed, IEventBus bus)
        {
            Content = content;
            Bus = bus;
            Time = new TimeState(1, seed);
            Rng = new RngStreams(seed);
            InstanceIds = new IdGenerator();
            ListingIds = new IdGenerator();
            Store = new InstanceStore();
            Market = new MarketState();
            Npcs = new NpcStateStore();
            AppraisalIds = new IdGenerator();
            Knowledge = new KnowledgeState();
            Equipment = new EquipmentState();

            EconomyState = new EconomyState(content.TransactionTypes);
            EconomyService = new EconomyService(EconomyState, content.EconomyConstants, bus);
            InventoryState = new InventoryState(content.EconomyConstants.InitialShelfCapacity);
            InventoryService = new InventoryService(InventoryState, Store, EconomyService, bus);

            Wealth = new WealthCalculator(new IWealthContributor[]
            {
                new CashWealthContributor(EconomyState),
                new StockWealthContributor(InventoryState, Store),
                new BusinessAssetsWealthContributor(EconomyState),
                new PendingAppraisalWealthContributor(EconomyState)
            });
            Summaries = new DaySummaryBuilder(EconomyState, content.TransactionTypes);
            LedgerView = new LedgerView(EconomyState, content.TransactionTypes);

            Appraisal = new AppraisalService(content, Knowledge, Equipment, EconomyService, Store, AppraisalIds, bus, seed);

            DemandState = new DemandState();
            Demand = new DemandModel(content.Demand, DemandState);
            CustomerIds = new IdGenerator();
            Customers = new CustomerService(content, new CustomerState(), CustomerIds, Store, InventoryState, Npcs, Demand, Time, Rng);
            TradeState = new TradeState();
            Trade = new TradeService(
                content, Market, Store, InventoryState, InventoryService, EconomyService, Knowledge, Npcs, Rng, Time, TradeState, bus, Customers);
            Sell = new SellService(content, Customers, TradeState, InventoryService, Store, Npcs, Demand, Knowledge, Time, bus);

            ListingGenerator = new ListingGenerator(content, Store, InstanceIds, ListingIds);
            var newDay = new NewDayStep(Time, Market, ListingGenerator, Rng, bus, new INewDayHook[] { Customers });
            DayEnd = new DayEndPipeline(new IDayEndStep[]
            {
                new MissedCustomersStep(Customers),
                new DailyExpenseStep(EconomyService),
                new ListingExpiryStep(Market, Store, EconomyService, bus),
                new DemandUpdateStep(Demand, Rng, content),
                newDay
            });
            Api = new GameApi(this);

            Result opened = EconomyService.OpenBooks();
            if (opened.IsFailure)
            {
                throw new InvalidOperationException("The ledger could not be opened: " + opened.Message);
            }

            Result firstDay = newDay.OpenFirstDay();
            if (firstDay.IsFailure)
            {
                throw new InvalidOperationException("The first day could not be opened: " + firstDay.Message);
            }
        }

        /// <summary>
        /// Yeni oyun: Gün 1, başlangıç sermayesi defterin 0. gününe yazılır, Gün 1 ilanları (rehberli ilan dahil) üretilir.
        /// İçerik bozuksa (doğrulayıcıdan geçtiği hâlde ilan üretilemiyorsa) InvalidOperationException atılır.
        /// </summary>
        /// <param name="events">Verilmezse oturum kendi olay veri yolunu kurar (<see cref="Bus"/>).</param>
        public static GameSession NewGame(ContentDatabase content, ulong seed, IEventBus events = null)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            return new GameSession(content, seed, events ?? new EventBus());
        }
    }
}
