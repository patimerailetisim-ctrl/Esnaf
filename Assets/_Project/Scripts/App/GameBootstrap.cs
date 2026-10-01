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

        [SerializeField]
        private int _seed = 20260101;

        private GameSession _session;
        private UiFlow _flow;
        private TopBarView _topBar;
        private ListingsView _listings;
        private DetailView _detail;

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

            Canvas canvas = UiBuilder.CreateCanvas("Canvas");
            UiBuilder.EnsureEventSystem();
            _listings = new ListingsView(canvas.transform, _flow);
            _detail = new DetailView(canvas.transform, _flow);
            _topBar = new TopBarView(canvas.transform);
            _flow.Changed += ShowScreen;
            ShowScreen();
        }

        private void OnDestroy()
        {
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
        }
    }
}
