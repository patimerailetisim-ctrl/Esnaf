using System;
using Esnaf.App.Ui;
using Esnaf.Content;
using Esnaf.Domain.Content;
using Esnaf.Domain.Game;
using Esnaf.Presentation;
using UnityEngine;

namespace Esnaf.App
{
    /// <summary>
    /// Oyunun Unity girişi: içeriği yükler, yeni oyun oturumu açar ve ekranları <see cref="UiFlow"/>'a bağlar.
    /// Oyun kuralı yoktur; ekranlar oyunla yalnızca UiFlow (IGameApi) üzerinden konuşur. Kaydet/yükle bu adımda yoktur.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private ContentCatalog _catalog;

        /// <summary>Gerçek telefon görselleri (isteğe bağlı). Atanmazsa ekspertiz ekranı mock telefonu çizer.</summary>
        [SerializeField]
        private PhoneImageCatalog _phoneImages;

        [SerializeField]
        private int _seed = 20260101;

        private GameSession _session;
        private UiFlow _flow;
        private TopBarView _topBar;
        private ListingsView _listings;
        private DetailView _detail;
        private AppraisalPanelView _appraisal;
        private NegotiationPanelView _negotiation;
        private ShelfView _shelf;
        private SalePanelView _sale;

        private void Awake()
        {
            if (_catalog == null)
            {
                Debug.LogError("GameBootstrap: ContentCatalog atanmamış. Esnaf > Setup Day 10 menüsünü çalıştırın.");
                return;
            }

            ContentLoadResult result = ContentDatabase.Load(_catalog.CreateContentSource());
            foreach (ContentIssue issue in result.Issues)
            {
                if (issue.Severity == ContentIssueSeverity.Error)
                {
                    Debug.LogError(issue.ToString());
                }
                else
                {
                    Debug.LogWarning(issue.ToString());
                }
            }

            if (!result.IsSuccess)
            {
                Debug.LogError("GameBootstrap: içerik geçersiz, oyun başlatılmadı (Esnaf > Validate Content).");
                return;
            }

            _session = GameSession.NewGame(result.Database, (ulong)_seed);
            _flow = new UiFlow(_session.Api, new ContentPresentation(result.Database), _session.Bus);

            PhoneImageCatalog phoneImages = _phoneImages;
#if UNITY_EDITOR
            if (phoneImages == null)
            {
                // Sahne eski kurulumdan kalmış olabilir (alan atanmamış): Editor'da kataloğu yoldan yükle.
                phoneImages = UnityEditor.AssetDatabase.LoadAssetAtPath<PhoneImageCatalog>("Assets/_Project/Art/Phones/PhoneImageCatalog.asset");
                if (phoneImages != null)
                {
                    Debug.LogWarning("[PhoneImages] GameBootstrap'te 'Phone Images' atanmamıştı; katalog Assets/_Project/Art/Phones/PhoneImageCatalog.asset yolundan yüklendi. Kalıcı çözüm: Esnaf > Setup Day 10.");
                }
            }
#endif
            Debug.Log("[PhoneImages] katalog: " + (phoneImages == null ? "YOK (mock kullanılacak)" : phoneImages.Describe()));
            PhoneImages.Provider = phoneImages == null ? null : (Func<string, PhoneAngle, Sprite>)phoneImages.GetSprite;

            Canvas canvas = UiBuilder.CreateCanvas("Canvas");
            UiBuilder.EnsureEventSystem();
            _listings = new ListingsView(canvas.transform, _flow);
            _detail = new DetailView(canvas.transform, _flow);
            _appraisal = new AppraisalPanelView(canvas.transform, _flow);
            _negotiation = new NegotiationPanelView(canvas.transform, _flow);
            _shelf = new ShelfView(canvas.transform, _flow);
            _sale = new SalePanelView(canvas.transform, _flow);
            _topBar = new TopBarView(canvas.transform);
            _flow.Changed += ShowScreen;
            ShowScreen();
        }

        /// <summary>
        /// Yalnızca el ile deneme yardımcısı (Play Mode'da bileşen başlığındaki ⋮ menüsü ya da üst menüde Esnaf > Test: ...): ilk ilanı istenen fiyattan alır ve etikete aynı fiyatı koyar,
        /// böylece Müşteriler ekranında müşteri çıkar. Yalnızca IGameApi'yi kullanır (BuyListing, SetPrice); oyun kuralı değildir.
        /// </summary>
        [ContextMenu("Test: ilk ilani al ve etiketle (musteri gelsin)")]
        public void DebugBuyAndPriceFirstListing()
        {
            if (_session == null || _flow == null)
            {
                Debug.LogWarning("GameBootstrap: oyun başlamamış (Play Mode'da çalıştırın).");
                return;
            }

            var listings = _session.Api.GetListings();
            if (listings.Count == 0)
            {
                Debug.LogWarning("GameBootstrap: ilan yok.");
                return;
            }

            Esnaf.Core.Money asking = listings[0].AskingPrice;
            var bought = _session.Api.BuyListing(listings[0].ListingId);
            if (bought.IsFailure)
            {
                Debug.LogWarning("GameBootstrap: satın alma olmadı: " + bought.ErrorCode);
                return;
            }

            var stock = _session.Api.GetInventory();
            long instanceId = stock[stock.Count - 1].InstanceId;
            var priced = _session.Api.SetPrice(instanceId, asking);
            Debug.Log("GameBootstrap: etiket " + (priced.IsSuccess ? "konuldu: " + asking.Tl + " TL" : "olmadı: " + priced.ErrorCode));
            _flow.Refresh();
        }

        private void OnDestroy()
        {
            PhoneImages.Provider = null;
            if (_flow != null)
            {
                _flow.Changed -= ShowScreen;
                _flow.Dispose();
            }
        }

        private void ShowScreen()
        {
            _topBar.Show(_flow.TopBar);
            _listings.Show();
            _detail.Show();
            _appraisal.Show();
            _negotiation.Show();
            _shelf.Show();
            _sale.Show();
        }
    }
}
