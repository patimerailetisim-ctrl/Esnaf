using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Inventory;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Time;
using Esnaf.Domain.Wholesale;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Arayüzün TEK arka kapısı: Unity ekranları oyunla yalnızca buradan konuşur (IGameApi üzerinden). Oyun kuralı içermez;
    /// durumu okur, ekranlara hazır metin/görünüm modeli verir ve değişince <see cref="Changed"/> ile haber verir.
    /// Olay yolu (isteğe bağlı) verilirse nakit ve gün olayları üst barı kendiliğinden günceller; verilmezse <see cref="Refresh"/> çağrılır.
    /// </summary>
    public sealed class UiFlow : IDisposable
    {
        private readonly IGameApi _api;
        private readonly ContentPresentation _content;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private long? _selectedListingId;
        private string _selectedLevelId;
        private string _offerText = string.Empty;
        private string _reply;
        private Money? _lastOffer;
        private Money _previousShown;
        private int _selectedCardIndex = -1;
        private readonly SaleUiState _sale = new SaleUiState();

        /// <summary>En büyük kabul edilen teklif kutusu değeri (TL). Gerçek doğrulama yine API'dedir.</summary>
        public const long MaxOffer = 1000000000L;
        private bool _disposed;

        public UiFlow(IGameApi api, ContentPresentation content, IEventBus events = null)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            _api = api;
            _content = content;
            CurrentScreen = UiScreen.Listings;
            Rebuild();
            if (events != null)
            {
                _subscriptions.Add(events.Subscribe<CashChanged>(e => Refresh()));
                _subscriptions.Add(events.Subscribe<DayStarted>(e => Refresh()));
                _subscriptions.Add(events.Subscribe<ListingsGenerated>(e => Refresh()));
                _subscriptions.Add(events.Subscribe<ListingExpired>(e => Refresh()));
                _subscriptions.Add(events.Subscribe<ListingPurchased>(e => Refresh()));
                _subscriptions.Add(events.Subscribe<NegotiationEnded>(e => Refresh()));
                _subscriptions.Add(events.Subscribe<ItemAddedToShelf>(e => Refresh()));
            }
        }

        /// <summary>Ekranın görünümü değişince çağrılır (ana iş parçacığında, olayı yayan çağrının içinde).</summary>
        public event Action Changed;

        public UiScreen CurrentScreen { get; private set; }

        /// <summary>Seçili ilanın telefon detayı satırları; seçili ilan yoksa null.</summary>
        public ListingDetailViewModel Detail { get; private set; }

        /// <summary>Ekspertiz ekranı (yalnızca CurrentScreen == Appraisal iken dolu; aksi halde null).</summary>
        public AppraisalScreenViewModel AppraisalScreen { get; private set; }

        /// <summary>Pazarlık ekranı (yalnızca CurrentScreen == Negotiation iken dolu; aksi halde null).</summary>
        public NegotiationScreenViewModel NegotiationScreen { get; private set; }

        /// <summary>Raf ekranı (yalnızca CurrentScreen == Shelf iken dolu; aksi halde null). Salt okunurdur.</summary>
        public ShelfScreenViewModel ShelfScreen { get; private set; }

        /// <summary>İlanlar ekranındaki Raf düğmesinin yazısı: "Raf (n/kapasite)" (IGameApi.GetInventory).</summary>
        public string ShelfButtonText { get; private set; }

        /// <summary>Müşteri satış ekranı (yalnızca CurrentScreen == Sale iken dolu; aksi halde null).</summary>
        public SaleScreenViewModel SaleScreen { get; private set; }

        /// <summary>İlanlar ekranındaki Müşteriler düğmesinin yazısı: "Müşteriler (n)" (IGameApi.GetCustomers).</summary>
        public string CustomersButtonText { get; private set; }

        /// <summary>Toptancı ekranı (yalnızca CurrentScreen == Wholesale iken dolu; aksi halde null).</summary>
        public WholesaleScreenViewModel WholesaleScreen { get; private set; }

        /// <summary>Aksesuar Stoğu ekranı (yalnızca CurrentScreen == AccessoryStock iken dolu; aksi halde null). Telefon rafından ayrıdır.</summary>
        public AccessoryStockScreenViewModel AccessoryStockScreen { get; private set; }

        /// <summary>İlanlar ekranındaki Aksesuar düğmesinin yazısı: "Aksesuar (X/60)" (aksesuar kapasitesi; telefon rafı değil).</summary>
        public string AccessoryButtonText { get; private set; }

        public TopBarViewModel TopBar { get; private set; }

        /// <summary>Pazardaki ilanlar (pazardaki sırayla), seçili olan işaretli.</summary>
        public IReadOnlyList<ListingRowViewModel> Listings { get; private set; }

        /// <summary>Seçili ilan (sonraki ekran, Telefon Detayı, bunu kullanır); yoksa null. İlan pazardan kalkınca seçim kalkar.</summary>
        public long? SelectedListingId
        {
            get { return _selectedListingId; }
        }

        public ListingRowViewModel SelectedListing
        {
            get
            {
                foreach (ListingRowViewModel row in Listings)
                {
                    if (row.IsSelected)
                    {
                        return row;
                    }
                }

                return null;
            }
        }

        /// <summary>Son komutun kullanıcıya gösterilecek hata mesajı (Türkçe); başarılı komut bunu temizler. Yoksa null.</summary>
        public string StatusMessage { get; private set; }

        public ContentPresentation Content
        {
            get { return _content; }
        }

        /// <summary>Durumu yeniden okur ve <see cref="Changed"/> olayını bir kez yayar.</summary>
        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            Rebuild();
            RaiseChanged();
        }

        /// <summary>İlanı seçer ve seçimi korur. İlan artık yoksa seçim değişmez, StatusMessage nedenini söyler. Olay bir kez yayılır.</summary>
        public bool SelectListing(long listingId)
        {
            bool exists = Select(listingId);
            Rebuild();
            RaiseChanged();
            return exists;
        }

        /// <summary>İlanı seçer ve Telefon Detayı ekranına geçer (tek olay). İlan yoksa ekran değişmez, StatusMessage nedenini söyler.</summary>
        public bool OpenListing(long listingId)
        {
            bool exists = Select(listingId);
            if (exists)
            {
                CurrentScreen = UiScreen.Detail;
            }

            Rebuild();
            RaiseChanged();
            return exists;
        }

        /// <summary>Seçili ilanın detayına geçer; seçili ilan yoksa hiçbir şey yapmaz (false).</summary>
        public bool OpenSelectedListing()
        {
            if (_selectedListingId == null)
            {
                return false;
            }

            CurrentScreen = UiScreen.Detail;
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>Bir adım geri: Ekspertiz → Detay, Pazarlık → Detay (pazarlık açık kalır), Detay → İlanlar, Raf → İlanlar (seçim korunur, mesaj temizlenir). İlanlar ekranındaysa hiçbir şey yapmaz.</summary>
        public void Back()
        {
            if (CurrentScreen == UiScreen.Appraisal)
            {
                CurrentScreen = UiScreen.Detail;
            }
            else if (CurrentScreen == UiScreen.Negotiation)
            {
                CurrentScreen = UiScreen.Detail; // pazarlık oyunda açık kalır; geri dönülünce kaldığı yerden sürer
            }
            else if (CurrentScreen == UiScreen.Detail || CurrentScreen == UiScreen.Shelf)
            {
                CurrentScreen = UiScreen.Listings;
            }
            else if (CurrentScreen == UiScreen.Wholesale || CurrentScreen == UiScreen.AccessoryStock)
            {
                CurrentScreen = UiScreen.Listings;
            }
            else if (CurrentScreen == UiScreen.Sale)
            {
                CurrentScreen = UiScreen.Listings; // süren satış oyunda açık kalır; Müşteriler'e dönünce kaldığı yerden sürer
                if (_api.GetSale() == null)
                {
                    ResetSaleState();
                }
            }
            else
            {
                return;
            }

            StatusMessage = null;
            Rebuild();
            RaiseChanged();
        }

        /// <summary>Detaydan Ekspertiz ekranına geçer. Detay ekranında değilse hiçbir şey yapmaz (false).</summary>
        public bool OpenAppraisal()
        {
            if (CurrentScreen != UiScreen.Detail)
            {
                return false;
            }

            CurrentScreen = UiScreen.Appraisal;
            _selectedLevelId = null;
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>Ekspertiz seviyesini seçer. Bilinmeyen seviye reddedilir (StatusMessage söyler). Ekspertiz ekranında değilse hiçbir şey yapmaz.</summary>
        public bool SelectLevel(string levelId)
        {
            if (CurrentScreen != UiScreen.Appraisal)
            {
                return false;
            }

            bool known = false;
            foreach (AppraisalLevelInfo level in _content.AppraisalLevels)
            {
                if (level.Id == levelId)
                {
                    known = true;
                }
            }

            if (known)
            {
                _selectedLevelId = levelId;
                StatusMessage = null;
            }
            else
            {
                StatusMessage = TurkishTexts.Error("appraisal.level_unknown");
            }

            Rebuild();
            RaiseChanged();
            return known;
        }

        /// <summary>
        /// Seçili seviyede, seçili ilan için ekspertiz yaptırır (IGameApi.StartAppraisal): ücret oyundan düşer, sonuç ilana bağlı ve kilitlidir
        /// (aynı seviye tekrar ücret almaz). Hata olursa StatusMessage Türkçe nedeni söyler; durum değişmez.
        /// </summary>
        public Result<AppraisalView> PerformAppraisal()
        {
            if (CurrentScreen != UiScreen.Appraisal) // bu ekranda seçili ilan her zaman vardır (Rebuild ekranı aksi halde kapatır)
            {
                return Result<AppraisalView>.Fail("ui.not_on_appraisal_screen", "The appraisal screen is not open.");
            }

            if (_selectedLevelId == null)
            {
                StatusMessage = TurkishTexts.NoLevelSelected;
                Rebuild();
                RaiseChanged();
                return Result<AppraisalView>.Fail("ui.no_level_selected", "No appraisal level is selected.");
            }

            Result<AppraisalView> result = _api.StartAppraisal(_selectedListingId.Value, _selectedLevelId);
            StatusMessage = result.IsSuccess ? null : TurkishTexts.Error(result.ErrorCode);
            Refresh();
            return result;
        }

        /// <summary>
        /// Detaydan Pazarlık ekranına geçer. Bu ilan için pazarlık zaten açıksa kaldığı yerden sürer; değilse IGameApi.StartNegotiation çağrılır
        /// (başka ilanın pazarlığı sürüyorsa API reddeder, StatusMessage söyler). Detay ekranında değilse hiçbir şey yapmaz (false).
        /// </summary>
        public bool OpenNegotiation()
        {
            if (CurrentScreen != UiScreen.Detail) // detay ekranında seçili ilan her zaman vardır (Rebuild ekranı aksi halde kapatır)
            {
                return false;
            }

            NegotiationView view = _api.GetNegotiation();
            if (view == null || view.ListingId != _selectedListingId.Value)
            {
                Result<NegotiationView> started = _api.StartNegotiation(_selectedListingId.Value);
                if (started.IsFailure)
                {
                    StatusMessage = TurkishTexts.Error(started.ErrorCode);
                    Rebuild();
                    RaiseChanged();
                    return false;
                }

                view = started.Value;
            }

            ResetNegotiationState();
            _offerText = view.ShownPrice.Tl.ToString(CultureInfo.InvariantCulture);
            _previousShown = view.ShownPrice;
            _reply = view.Phase == NegotiationPhase.FinalOffer ? TurkishTexts.ReplyFinal(view.ShownPrice)
                : view.Round == 0 ? TurkishTexts.ReplyOpening(view.ShownPrice)
                : TurkishTexts.ReplyHolding(view.ShownPrice);
            CurrentScreen = UiScreen.Negotiation;
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>Teklif kutusunun yazısı. Olay yayılmaz (yazarken ekran yeniden kurulmasın); yalnızca saklanır.</summary>
        public void SetOfferText(string text)
        {
            _offerText = text ?? string.Empty;
            if (CurrentScreen == UiScreen.Negotiation)
            {
                Rebuild();
            }
        }

        /// <summary>Teklif kutusunun yazısını <paramref name="delta"/> TL artırır/azaltır (en az 10, en çok <see cref="MaxOffer"/>); yalnızca kutuyu düzenler, oyuna dokunmaz.</summary>
        public void AdjustOffer(int delta)
        {
            if (CurrentScreen != UiScreen.Negotiation)
            {
                return;
            }

            Money current;
            long baseValue = ParseOffer(_offerText, out current) == null ? current.Tl : 0L;
            long value = Math.Min(MaxOffer, Math.Max(10L, baseValue + delta));
            _offerText = value.ToString(CultureInfo.InvariantCulture);
            Rebuild();
            RaiseChanged();
        }

        /// <summary>Koz kartını seçer; aynı kart tekrar seçilirse seçim kalkar. Geçersiz sıra reddedilir (false).</summary>
        public bool SelectCard(int index)
        {
            NegotiationView view = CurrentScreen == UiScreen.Negotiation ? _api.GetNegotiation() : null;
            if (view == null || index < 0 || index >= view.Cards.Count)
            {
                return false;
            }

            _selectedCardIndex = _selectedCardIndex == index ? -1 : index;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>"Teklif Ver": kutudaki tutarı IGameApi.MakeOffer'a gönderir. Anlaşılırsa satın alma oyunda olur; sonuç Türkçe gösterilir.</summary>
        public Result<NegotiationView> SubmitOffer()
        {
            Money offer;
            Result<NegotiationView> failure;
            if (!TryGetOffer(out offer, out failure))
            {
                return failure;
            }

            return Send(() => _api.MakeOffer(offer), offer);
        }

        /// <summary>"Koz Kullan": seçili kartla birlikte teklifi IGameApi.MakeOfferWithCard'a gönderir.</summary>
        public Result<NegotiationView> SubmitCardOffer()
        {
            NegotiationView view = CurrentScreen == UiScreen.Negotiation ? _api.GetNegotiation() : null;
            if (view == null)
            {
                return Result<NegotiationView>.Fail("ui.not_on_negotiation_screen", "The negotiation screen is not open.");
            }

            if (_selectedCardIndex < 0) // seçim SelectCard'da doğrulanır; kart listesi pazarlık boyunca küçülmez
            {
                return Refuse("ui.no_card_selected", TurkishTexts.NoCardSelected);
            }

            Money offer;
            Result<NegotiationView> failure;
            if (!TryGetOffer(out offer, out failure))
            {
                return failure;
            }

            NegotiationCardView card = view.Cards[_selectedCardIndex];
            return Send(() => _api.MakeOfferWithCard(offer, card.AppraisalId, card.CardIndex), offer);
        }

        /// <summary>"Son Fiyatı Kabul Et": IGameApi.AcceptFinalPrice. Satıcı son fiyat vermediyse API reddeder.</summary>
        public Result<NegotiationView> AcceptFinalPrice()
        {
            if (CurrentScreen != UiScreen.Negotiation)
            {
                return Result<NegotiationView>.Fail("ui.not_on_negotiation_screen", "The negotiation screen is not open.");
            }

            return Send(() => _api.AcceptFinalPrice(), null);
        }

        /// <summary>"Vazgeç": IGameApi.WalkAway. Mevcut sistemde ilan pazardan kalkar; para değişmez.</summary>
        public Result<NegotiationView> WalkAway()
        {
            if (CurrentScreen != UiScreen.Negotiation)
            {
                return Result<NegotiationView>.Fail("ui.not_on_negotiation_screen", "The negotiation screen is not open.");
            }

            Result<NegotiationView> result = _api.WalkAway();
            StatusMessage = result.IsSuccess ? TurkishTexts.WalkedAway : TurkishTexts.Error(result.ErrorCode);
            Refresh();
            return result;
        }

        /// <summary>İlanlar ekranından salt okunur Raf ekranını açar. İlanlar ekranında değilse hiçbir şey yapmaz (false).</summary>
        public bool OpenShelf()
        {
            if (CurrentScreen != UiScreen.Listings)
            {
                return false;
            }

            CurrentScreen = UiScreen.Shelf;
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>İlanlar (ya da Aksesuar Stoğu) ekranından Toptancı ekranını açar. Başka ekrandaysa hiçbir şey yapmaz (false).</summary>
        public bool OpenWholesale()
        {
            if (CurrentScreen != UiScreen.Listings && CurrentScreen != UiScreen.AccessoryStock)
            {
                return false;
            }

            CurrentScreen = UiScreen.Wholesale;
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>İlanlar (ya da Toptancı) ekranından Aksesuar Stoğu ekranını açar. Başka ekrandaysa hiçbir şey yapmaz (false).</summary>
        public bool OpenAccessoryStock()
        {
            if (CurrentScreen != UiScreen.Listings && CurrentScreen != UiScreen.Wholesale)
            {
                return false;
            }

            CurrentScreen = UiScreen.AccessoryStock;
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Toptancıdan bir paket alır (IGameApi.BuyWholesalePack; tek kaynak WholesaleService). Başarıda "10 adet Şarj Adaptörü stoğa eklendi." der;
        /// hatada (nakit, kapasite, gün kilidi...) Türkçe nedeni söyler ve hiçbir şey değişmez. Yalnızca Toptancı ekranında çalışır.
        /// </summary>
        public Result<WholesalePurchaseReceipt> BuyWholesalePack(string supplierId, string accessoryId)
        {
            if (CurrentScreen != UiScreen.Wholesale)
            {
                return Result<WholesalePurchaseReceipt>.Fail("ui.not_on_wholesale_screen", "The wholesale screen is not open.");
            }

            string name = accessoryId;
            int opensOn = 1;
            foreach (WholesaleOfferView offer in _api.GetWholesaleOffers())
            {
                if (offer.SupplierId == supplierId && offer.AccessoryId == accessoryId)
                {
                    name = offer.AccessoryName;
                    opensOn = offer.AvailableFromDay;
                }
            }

            Result<WholesalePurchaseReceipt> result = _api.BuyWholesalePack(supplierId, accessoryId);
            StatusMessage = result.IsSuccess
                ? TurkishTexts.PackAdded(result.Value.Quantity, name)
                : TurkishTexts.WholesaleError(result.ErrorCode, opensOn);
            Refresh();
            return result;
        }

        /// <summary>
        /// İlanlar ekranından Müşteri satış ekranını açar. Süren bir satış varsa kaldığı yerden sürer; yoksa dükkândaki müşteriler listelenir.
        /// İlanlar ekranında değilse hiçbir şey yapmaz (false).
        /// </summary>
        public bool OpenCustomers()
        {
            if (CurrentScreen != UiScreen.Listings)
            {
                return false;
            }

            CurrentScreen = UiScreen.Sale;
            StatusMessage = null;
            SaleView running = _api.GetSale();
            if (running != null && !_sale.Initialized)
            {
                ResumeSale(running);
            }

            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>Müşteriyle konuşmaya başlar (IGameApi.StartSale). Hata olursa StatusMessage Türkçe nedeni söyler. Satış ekranında değilse hiçbir şey yapmaz.</summary>
        public Result<SaleView> StartSale(long customerId)
        {
            if (CurrentScreen != UiScreen.Sale)
            {
                return Result<SaleView>.Fail("ui.not_on_sale_screen", "The sale screen is not open.");
            }

            Result<SaleView> result = _api.StartSale(customerId);
            if (result.IsFailure)
            {
                StatusMessage = TurkishTexts.Error(result.ErrorCode);
                Refresh();
                return result;
            }

            ResetSaleState();
            SaleView view = result.Value;
            _sale.Initialized = true;
            _sale.DefinitionId = DefinitionOf(view.InstanceId);
            _sale.AskTl = DefaultAsk(view.ShownPrice);
            _sale.CustomerLine = SaleDialogue.Greeting(PersonalityOf(view), _content.ModelName(_sale.DefinitionId));
            StatusMessage = null;
            Refresh();
            return result;
        }

        /// <summary>"Tabii abi, buyur.": yalnızca konuşmayı ilerletir (oyuna dokunmaz); müşteri fiyatı sorar.</summary>
        public bool SaleGreet()
        {
            SaleView view = CurrentScreen == UiScreen.Sale ? _api.GetSale() : null;
            if (view == null || _sale.Stage != 0)
            {
                return false;
            }

            _sale.Stage = 1;
            _sale.PlayerLine = TurkishTexts.ReplyGreet;
            _sale.CustomerLine = SaleDialogue.AfterGreeting(view.Profile == null ? NegotiationLevel.Medium : view.Profile.Knowledge);
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>Fiyat seçicisini <paramref name="delta"/> TL kaydırır (en az 10, en çok <see cref="MaxOffer"/>); yalnızca ekran değeridir, oyuna dokunmaz.</summary>
        public void AdjustSalePrice(int delta)
        {
            if (CurrentScreen != UiScreen.Sale || _api.GetSale() == null)
            {
                return;
            }

            _sale.AskTl = Math.Min(MaxOffer, Math.Max(10L, _sale.AskTl + delta));
            Rebuild();
            RaiseChanged();
        }

        /// <summary>"6.500 ₺ olur abi.": seçili fiyatı IGameApi.AskPrice'a gönderir. Anlaşma olursa satış oyunda yapılmıştır.</summary>
        public Result<SaleView> SaleAsk()
        {
            Money ask = Money.FromTl(_sale.AskTl);
            return SendSale(() => _api.AskPrice(ask), TurkishTexts.ReplyPrice(ask), SaleSpeech.Ask);
        }

        /// <summary>"Ekspertizi yapıldı…": raporu müşteriye gösterir (IGameApi.ShowReport).</summary>
        public Result<SaleView> SaleShowReport(long appraisalId)
        {
            return SendSale(() => _api.ShowReport(appraisalId), TurkishTexts.ReplyReport, SaleSpeech.Report);
        }

        /// <summary>"Teklifi Kabul Et": müşterinin şu anki teklifini aynen kabul eder (IGameApi.AcceptCustomerOffer).</summary>
        public Result<SaleView> SaleAcceptFinal()
        {
            SaleView view = CurrentScreen == UiScreen.Sale ? _api.GetSale() : null;
            string line = view == null ? string.Empty : TurkishTexts.ReplyAcceptFinal(view.ShownPrice);
            return SendSale(() => _api.AcceptCustomerOffer(), line, SaleSpeech.Ask);
        }

        /// <summary>"Olmadı abi, başka sefere.": müşteriyi yolcu eder (IGameApi.LetCustomerGo); ürün rafta kalır.</summary>
        public Result<SaleView> SaleLetGo()
        {
            return SendSale(() => _api.LetCustomerGo(), TurkishTexts.ReplyLetGo, SaleSpeech.Ask);
        }

        /// <summary>
        /// Biten (anlaşmalı) telefon satışına bir aksesuar ekler (IGameApi.SellAccessoryAddOn; fiyat, stok ve kâr oyundan gelir, burada kural yoktur).
        /// Telefon satışı değişmez. Başarıda "… eklendi (+120 ₺)." der, hatada Türkçe nedeni söyler; ikisi de panelde gösterilir.
        /// </summary>
        public Result<AccessorySaleReceipt> SaleAddAccessory(string accessoryId)
        {
            AddOnPanelViewModel panel = CurrentScreen == UiScreen.Sale && SaleScreen != null ? SaleScreen.AddOn : null;
            if (panel == null)
            {
                return Result<AccessorySaleReceipt>.Fail("ui.no_addon_panel", "The accessory add-on panel is not open.");
            }

            string name = accessoryId;
            foreach (AddOnCardViewModel card in panel.Cards)
            {
                if (card.AccessoryId == accessoryId)
                {
                    name = card.Name;
                }
            }

            Result<AccessorySaleReceipt> result = _api.SellAccessoryAddOn(accessoryId);
            _sale.AddOnFeedbackIsError = result.IsFailure;
            _sale.AddOnFeedback = result.IsSuccess ? TurkishTexts.AddOnAdded(name, result.Value.SalePrice) : TurkishTexts.AddOnError(result.ErrorCode);
            Refresh();
            return result;
        }

        /// <summary>Biten satıştan müşteri listesine döner (yalnızca arayüz durumunu temizler).</summary>
        public bool SaleNext()
        {
            if (CurrentScreen != UiScreen.Sale || _api.GetSale() != null || _sale.Final == null)
            {
                return false;
            }

            ResetSaleState();
            StatusMessage = null;
            Rebuild();
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// "Satın Al": seçili ilanı pazarlıksız, İSTENEN fiyattan satın alır (IGameApi.BuyListing). Para, raf ve ilan listesi oyundan okunur;
        /// başarıda İlanlar ekranına döner ve "Satın alındı … Rafa eklendi" der, hatada (nakit, raf dolu, pazarlık sürüyor…) Türkçe nedeni söyler.
        /// Yalnızca Telefon Detayı ekranında çalışır.
        /// </summary>
        public Result<Money> BuyNow()
        {
            if (CurrentScreen != UiScreen.Detail)
            {
                return Result<Money>.Fail("ui.not_on_detail_screen", "The detail screen is not open.");
            }

            string model = Detail.Title;
            Result<Money> result = _api.BuyListing(_selectedListingId.Value);
            StatusMessage = result.IsSuccess
                ? TurkishTexts.Purchased(model, result.Value, _api.GetInventory().Count, _content.ShelfCapacity)
                : TurkishTexts.Error(result.ErrorCode);
            Refresh();
            return result;
        }

        public void ClearSelection()
        {
            if (_selectedListingId == null)
            {
                return;
            }

            _selectedListingId = null;
            Rebuild(); // seçim yoksa Rebuild ekranı İlanlar'a döndürür
            RaiseChanged();
        }

        /// <summary>Günü bitirir (IGameApi.EndDay) ve ilanları/üst barı yeniler. Hata olursa StatusMessage Türkçe nedeni söyler.</summary>
        public Result<DayEndReport> EndDay()
        {
            Result<DayEndReport> result = _api.EndDay();
            StatusMessage = result.IsSuccess ? null : TurkishTexts.Error(result.ErrorCode);
            Refresh();
            return result;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }

        private enum SaleSpeech
        {
            Ask,
            Report
        }

        // Satış komutunu API'ye gönderir; sonucu ekrana yansıtır. Anlaşma/ayrılma ile satış biterse son görünüm saklanır (ekran "bitti" gösterir).
        private Result<SaleView> SendSale(Func<Result<SaleView>> command, string playerLine, SaleSpeech speech)
        {
            if (CurrentScreen != UiScreen.Sale || _api.GetSale() == null)
            {
                return Result<SaleView>.Fail("ui.no_sale", "There is no sale in progress.");
            }

            Result<SaleView> result = command();
            if (result.IsFailure)
            {
                StatusMessage = TurkishTexts.Error(result.ErrorCode);
                Refresh();
                return result;
            }

            SaleView view = result.Value;
            StatusMessage = null;
            _sale.PlayerLine = playerLine;
            NegotiationLevel mood = view.Mood;
            if (view.Phase == NegotiationPhase.Deal)
            {
                _sale.Final = view;
                _sale.CustomerLine = SaleDialogue.Deal(view.DealPrice);
            }
            else if (view.Phase == NegotiationPhase.Failed)
            {
                _sale.Final = view;
                _sale.CustomerLine = SaleDialogue.Left();
            }
            else if (speech == SaleSpeech.Report)
            {
                _sale.CustomerLine = SaleDialogue.AfterReport(mood);
            }
            else
            {
                NegotiationLevel haggling = view.Profile == null ? NegotiationLevel.Medium : view.Profile.Haggling;
                _sale.CustomerLine = SaleDialogue.AfterAsk(view.Phase, view.ShownPrice, view.LastAskTooExpensive, mood, view.Patience, haggling);
            }

            Refresh();
            return result;
        }

        // Yeni açılan oturumda (ör. UiFlow yeniden kuruldu) süren satış için ekran durumunu oyundan kurar.
        private void ResumeSale(SaleView view)
        {
            ResetSaleState();
            _sale.Initialized = true;
            _sale.DefinitionId = DefinitionOf(view.InstanceId);
            _sale.AskTl = DefaultAsk(view.ShownPrice);
            _sale.Stage = view.Round > 0 || view.Phase != NegotiationPhase.Active ? 1 : 0;
            _sale.CustomerLine = _sale.Stage == 0
                ? SaleDialogue.Greeting(PersonalityOf(view), _content.ModelName(_sale.DefinitionId))
                : SaleDialogue.AfterAsk(view.Phase, view.ShownPrice, view.LastAskTooExpensive, view.Mood, view.Patience, view.Profile == null ? NegotiationLevel.Medium : view.Profile.Haggling);
        }

        private void ResetSaleState()
        {
            _sale.Stage = 0;
            _sale.AskTl = 0L;
            _sale.CustomerLine = null;
            _sale.PlayerLine = null;
            _sale.Final = null;
            _sale.DefinitionId = null;
            _sale.AddOnFeedback = null;
            _sale.AddOnFeedbackIsError = false;
            _sale.Initialized = false;
        }

        private static string PersonalityOf(SaleView view)
        {
            return view.Profile == null ? null : view.Profile.PersonalityId;
        }

        private string DefinitionOf(long instanceId)
        {
            foreach (StockLine line in _api.GetInventory())
            {
                if (line.InstanceId == instanceId)
                {
                    return line.DefinitionId;
                }
            }

            return null;
        }

        // Fiyat seçicinin başlangıcı: müşterinin görünen teklifinin biraz üstü, 100 ₺'ye yukarı yuvarlı (yalnızca ekran varsayılanı; karar oyundadır).
        private static long DefaultAsk(Money shown)
        {
            long up = (long)Math.Ceiling(shown.Tl * 1.2 / SaleScreenBuilder.PriceStep) * SaleScreenBuilder.PriceStep;
            return Math.Max(10L, up);
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        private bool TryGetOffer(out Money offer, out Result<NegotiationView> failure)
        {
            failure = default(Result<NegotiationView>);
            if (CurrentScreen != UiScreen.Negotiation)
            {
                offer = Money.Zero;
                failure = Result<NegotiationView>.Fail("ui.not_on_negotiation_screen", "The negotiation screen is not open.");
                return false;
            }

            string problem = ParseOffer(_offerText, out offer);
            if (problem == null)
            {
                return true;
            }

            failure = Refuse("ui.offer_invalid", problem);
            return false;
        }

        private Result<NegotiationView> Refuse(string code, string message)
        {
            StatusMessage = message;
            Rebuild();
            RaiseChanged();
            return Result<NegotiationView>.Fail(code, message);
        }

        // Teklif kutusu yazısını sayıya çevirir: yalnızca rakamlar, 1..MaxOffer. Sorun varsa Türkçe mesaj döner (API'ye gidilmez), yoksa null.
        // 10 TL'nin katı olma ve diğer oyun kuralları burada KONTROL EDİLMEZ: onlar API'nin işidir.
        private static string ParseOffer(string text, out Money offer)
        {
            offer = Money.Zero;
            string trimmed = text == null ? string.Empty : text.Trim();
            if (trimmed.Length == 0)
            {
                return TurkishTexts.OfferEmpty;
            }

            if (trimmed[0] == '-')
            {
                return TurkishTexts.OfferNegative;
            }

            foreach (char c in trimmed)
            {
                if (c < '0' || c > '9')
                {
                    return TurkishTexts.OfferNotNumber;
                }
            }

            string digits = trimmed.TrimStart('0');
            if (digits.Length == 0)
            {
                return TurkishTexts.OfferNotPositive;
            }

            if (digits.Length > 10)
            {
                return TurkishTexts.OfferTooLarge;
            }

            long value = long.Parse(digits, CultureInfo.InvariantCulture);
            if (value > MaxOffer)
            {
                return TurkishTexts.OfferTooLarge;
            }

            offer = Money.FromTl(value);
            return null;
        }

        // Komutu API'ye gönderir; sonucu ekrana yansıtır. Anlaşma (Deal) olursa satın alma oyunda yapılmıştır: mesaj nakit/raf durumundan gelir.
        private Result<NegotiationView> Send(Func<Result<NegotiationView>> command, Money? offer)
        {
            string model = NegotiationScreen == null ? string.Empty : NegotiationScreen.Title;
            Money previousShown = _previousShown;
            Result<NegotiationView> result = command();
            if (result.IsFailure)
            {
                StatusMessage = TurkishTexts.Error(result.ErrorCode);
                Refresh();
                return result;
            }

            NegotiationView view = result.Value;
            if (offer.HasValue)
            {
                _lastOffer = offer;
            }

            if (view.Phase == NegotiationPhase.Deal)
            {
                StatusMessage = TurkishTexts.Purchased(model, view.DealPrice, _api.GetInventory().Count, _content.ShelfCapacity);
            }
            else
            {
                StatusMessage = null;
                _reply = TurkishTexts.Reply(view.Phase, view.ShownPrice, previousShown);
                _previousShown = view.ShownPrice;
                _selectedCardIndex = -1;
            }

            Refresh();
            return result;
        }

        // Ekran durumu yalnızca Pazarlık ekranı açıkken okunur; her açılışta (yeni ya da devam eden pazarlık) sıfırdan kurulur.
        private void ResetNegotiationState()
        {
            _offerText = string.Empty;
            _reply = null;
            _lastOffer = null;
            _previousShown = Money.Zero;
            _selectedCardIndex = -1;
        }

        private bool Select(long listingId)
        {
            bool exists = false;
            foreach (ListingView listing in _api.GetListings())
            {
                if (listing.ListingId == listingId)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
            {
                _selectedListingId = listingId;
                StatusMessage = null;
            }
            else
            {
                StatusMessage = TurkishTexts.Error("listing.unknown");
            }

            return exists;
        }

        // Üst barı, ilan satırlarını ve detayı oyundan yeniden okur; seçili ilan artık pazarda değilse seçimi bırakır ve ilanlara döner.
        private void Rebuild()
        {
            TopBar = BuildTopBar();
            var rows = new List<ListingRowViewModel>();
            ListingView selectedListing = null;
            foreach (ListingView listing in _api.GetListings())
            {
                bool selected = _selectedListingId.HasValue && listing.ListingId == _selectedListingId.Value;
                if (selected)
                {
                    selectedListing = listing;
                }

                rows.Add(ListingRowViewModel.From(listing, _content, selected));
            }

            if (selectedListing == null)
            {
                _selectedListingId = null;
                _selectedLevelId = null;
                if (CurrentScreen != UiScreen.Shelf && CurrentScreen != UiScreen.Sale
                    && CurrentScreen != UiScreen.Wholesale && CurrentScreen != UiScreen.AccessoryStock)
                {
                    CurrentScreen = UiScreen.Listings;
                }

                Detail = null;
                AppraisalScreen = null;
                NegotiationScreen = null;
            }
            else
            {
                Detail = ListingDetailViewModel.From(selectedListing, _content);
                AppraisalScreen = CurrentScreen == UiScreen.Appraisal
                    ? AppraisalScreenBuilder.Build(selectedListing, _api.GetAppraisals(selectedListing.ListingId), _api.GetDay(), _selectedLevelId, _content, _api)
                    : null;
                NegotiationScreen = null;
                if (CurrentScreen == UiScreen.Negotiation)
                {
                    NegotiationView view = _api.GetNegotiation();
                    if (view == null || view.ListingId != selectedListing.ListingId)
                    {
                        CurrentScreen = UiScreen.Detail; // bu ilanın pazarlığı artık yok: eski pazarlık görünmez
                    }
                    else
                    {
                        NegotiationScreen = NegotiationScreenBuilder.Build(view, selectedListing, _api.GetCash(), _reply, _lastOffer, _offerText, _selectedCardIndex, _content);
                    }
                }
            }

            Listings = new ReadOnlyCollection<ListingRowViewModel>(rows);

            IReadOnlyList<StockLine> stock = _api.GetInventory();
            ShelfButtonText = TurkishTexts.ShelfButton(stock.Count, _content.ShelfCapacity);
            ShelfScreen = CurrentScreen == UiScreen.Shelf ? BuildShelf(stock) : null;
            CustomersButtonText = TurkishTexts.CustomersButton(_api.GetCustomers().Count);
            AccessoryStockView accessories = _api.GetAccessoryStock();
            AccessoryButtonText = TurkishTexts.AccessoryStockButton(accessories.TotalUnits, accessories.Capacity);
            WholesaleScreen = CurrentScreen == UiScreen.Wholesale ? WholesaleScreenBuilder.BuildWholesale(_api) : null;
            AccessoryStockScreen = CurrentScreen == UiScreen.AccessoryStock ? WholesaleScreenBuilder.BuildStock(_api) : null;
            SaleScreen = CurrentScreen == UiScreen.Sale ? SaleScreenBuilder.Build(_api, _content, _sale) : null;
        }

        private ShelfScreenViewModel BuildShelf(IReadOnlyList<StockLine> stock)
        {
            var items = new List<ShelfItemRowViewModel>();
            foreach (StockLine line in stock)
            {
                items.Add(new ShelfItemRowViewModel(_content.ModelName(line.DefinitionId), TurkishTexts.ShelfCost(line.CostBasis)));
            }

            return new ShelfScreenViewModel(
                TurkishTexts.ShelfTitle,
                TurkishTexts.ShelfCapacity(stock.Count, _content.ShelfCapacity),
                items,
                items.Count == 0 ? TurkishTexts.ShelfEmpty : null);
        }

        private TopBarViewModel BuildTopBar()
        {
            return new TopBarViewModel(TurkishTexts.Day(_api.GetDay()), TurkishTexts.Cash(_api.GetCash()));
        }
    }
}
