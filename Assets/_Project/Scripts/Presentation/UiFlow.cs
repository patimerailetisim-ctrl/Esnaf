using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Time;

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
            }
        }

        /// <summary>Ekranın görünümü değişince çağrılır (ana iş parçacığında, olayı yayan çağrının içinde).</summary>
        public event Action Changed;

        public UiScreen CurrentScreen { get; private set; }

        /// <summary>Seçili ilanın telefon detayı satırları; seçili ilan yoksa null.</summary>
        public ListingDetailViewModel Detail { get; private set; }

        /// <summary>Ekspertiz ekranı (yalnızca CurrentScreen == Appraisal iken dolu; aksi halde null).</summary>
        public AppraisalScreenViewModel AppraisalScreen { get; private set; }

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

        /// <summary>Bir adım geri: Ekspertiz → Detay, Detay → İlanlar (seçim korunur, mesaj temizlenir). İlanlar ekranındaysa hiçbir şey yapmaz.</summary>
        public void Back()
        {
            if (CurrentScreen == UiScreen.Appraisal)
            {
                CurrentScreen = UiScreen.Detail;
            }
            else if (CurrentScreen == UiScreen.Detail)
            {
                CurrentScreen = UiScreen.Listings;
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

        /// <summary>"Pazarlık" düğmesi: sonraki adımda uygulanacak; şimdilik yalnızca nedenini söyler, oyunu değiştirmez.</summary>
        public void RequestNegotiation()
        {
            Announce(TurkishTexts.NegotiationComingSoon);
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

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
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

        private void Announce(string message)
        {
            if (CurrentScreen != UiScreen.Detail)
            {
                return;
            }

            StatusMessage = message;
            RaiseChanged();
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
                CurrentScreen = UiScreen.Listings;
                Detail = null;
                AppraisalScreen = null;
            }
            else
            {
                Detail = ListingDetailViewModel.From(selectedListing, _content);
                AppraisalScreen = CurrentScreen == UiScreen.Appraisal
                    ? AppraisalScreenBuilder.Build(selectedListing, _api.GetAppraisals(selectedListing.ListingId), _api.GetDay(), _selectedLevelId, _content, _api)
                    : null;
            }

            Listings = new ReadOnlyCollection<ListingRowViewModel>(rows);
        }

        private TopBarViewModel BuildTopBar()
        {
            return new TopBarViewModel(TurkishTexts.Day(_api.GetDay()), TurkishTexts.Cash(_api.GetCash()));
        }
    }
}
