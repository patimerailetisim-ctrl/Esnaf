using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Global müşteri bildirimi (Gün 13.1): üst barın hemen altında, tüm ekranların ÜSTÜNDE görünen "Müşteri geldi: Ad" / "Müşteri ayrıldı: Ad" kartları.
    /// Yalnızca <see cref="UiFlow.Notices"/>'i çizer; "Müşteriye Git" → <see cref="UiFlow.GoToCustomer"/>, "Kapat" → <see cref="UiFlow.DismissNotice"/>. Ekranı kendiliğinden değiştirmez.
    /// Kök nesnenin kendisi tıklamayı engellemez (Image yok); yalnızca kartlar ve düğmeler engeller.
    /// </summary>
    internal sealed class CustomerNoticeView
    {
        private const float CardHeight = 210f;

        private readonly UiFlow _flow;
        private readonly RectTransform _root;

        public CustomerNoticeView(Transform canvas, UiFlow flow)
        {
            _flow = flow;
            var go = new GameObject("CustomerNotices", typeof(RectTransform));
            _root = go.GetComponent<RectTransform>();
            _root.SetParent(canvas, false);
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -TopBarView.Height);
            _root.sizeDelta = new Vector2(0f, 0f);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(24, 24, 12, 0);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void Show()
        {
            UiKit.Clear(_root); // Play Mode'da Destroy, Edit Mode'da (testler) DestroyImmediate
            _root.gameObject.SetActive(_flow.Notices.Count > 0);
            foreach (CustomerNoticeViewModel notice in _flow.Notices)
            {
                AddCard(notice);
            }

            _root.SetAsLastSibling(); // her zaman diğer ekranların üstünde
        }

        private void AddCard(CustomerNoticeViewModel notice)
        {
            long id = notice.CustomerId;
            RectTransform card = UiKit.Card(_root, "Notice_" + id, notice.IsLeft ? UiTheme.WarnSoft : UiTheme.GoldSoft);
            UiKit.Size2(card, 0f, CardHeight);

            RectTransform portrait = UiKit.Rounded(card, "Portrait", UiTheme.CardSoft);
            portrait.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(portrait, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -80f), new Vector2(180f, 80f));
            portrait.gameObject.AddComponent<RectMask2D>();
            CustomerPortraitView.Draw(portrait, notice.Name, notice.NpcId);

            Text title = UiKit.Label(card, "NoticeTitle", notice.Title, 40, UiTheme.Ink, true);
            UiBuilder.Stretch(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(204f, 6f), new Vector2(-390f, 90f));
            Text line = UiKit.Label(card, "NoticeLine", notice.Line, 32, UiTheme.Muted);
            UiBuilder.Stretch(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(204f, -92f), new Vector2(-390f, 2f));

            if (notice.CanGo)
            {
                Button go = UiKit.RoundedButton(card, "GoToCustomerButton", notice.GoButtonText, 36, UiTheme.Gold, UiTheme.Ink, () => _flow.GoToCustomer(id));
                UiBuilder.Stretch(go.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-366f, 6f), new Vector2(-20f, 92f));
                go.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            }

            Button dismiss = UiKit.RoundedButton(card, "DismissNoticeButton", notice.DismissButtonText, 32, UiTheme.Card, UiTheme.Ink, () => _flow.DismissNotice(id));
            UiBuilder.Stretch(dismiss.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-366f, -92f), new Vector2(-20f, -14f));
        }
    }
}
