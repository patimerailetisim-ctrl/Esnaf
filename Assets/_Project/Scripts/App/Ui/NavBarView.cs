using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Kalıcı alt navigasyon (Gün 13.4 revizyon): Dükkan | Toptancı | İlanlar | Profil — ikon üstte, yazı altta, 4 eşit alan. Koyu zemin, ince üst çizgi; aktif sekme altın renkte
    /// ve üstünde küçük altın vurgu çizgisi var, pasif sekmeler açık gri. Tüm ekranların altında sabit durur. Yalnızca <see cref="UiFlow.Nav"/>'ı çizer ve
    /// <see cref="UiFlow.GoToTab"/>'ı çağırır; oyun kuralı yoktur. Emoji yok; ikonlar <see cref="NavIcons"/> ile çizilir.
    /// </summary>
    internal sealed class NavBarView
    {
        public const float Height = 150f;

        private static readonly Color BarColor = new Color(0.055f, 0.075f, 0.13f, 1f);

        private readonly UiFlow _flow;
        private readonly RectTransform _bar;

        public NavBarView(Transform canvas, UiFlow flow)
        {
            _flow = flow;
            _bar = UiBuilder.CreatePanel(canvas, "NavBar", BarColor);
            UiBuilder.Stretch(_bar, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, Height));
        }

        public void Show()
        {
            UiKit.Clear(_bar); // Play Mode'da Destroy, Edit Mode'da (testler) DestroyImmediate
            _bar.SetAsLastSibling();

            // ince üst çizgi
            RectTransform border = UiBuilder.CreatePanel(_bar, "TopBorder", KarbazDark.Border);
            border.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(border, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), Vector2.zero);

            int index = 0;
            foreach (NavItemViewModel item in _flow.Nav.Items)
            {
                NavTab tab = item.Tab;
                Color tone = item.IsActive ? KarbazDark.Gold : KarbazDark.NavInactive;

                // tıklanabilir alan (görünmez, tüm sekmeyi kaplar)
                Button button = UiBuilder.CreateButton(_bar, "Nav_" + tab, string.Empty, 30, Color.clear, () => _flow.GoToTab(tab));
                button.GetComponentInChildren<Text>().gameObject.SetActive(false);
                RectTransform area = button.GetComponent<RectTransform>();
                float min = index * 0.25f;
                UiBuilder.Stretch(area, new Vector2(min, 0f), new Vector2(min + 0.25f, 1f), Vector2.zero, new Vector2(0f, -2f));

                // aktif sekme vurgusu: üstte küçük altın çizgi
                if (item.IsActive)
                {
                    RectTransform bar = UiKit.Rounded(area, "ActiveBar", KarbazDark.Gold);
                    bar.GetComponent<Image>().raycastTarget = false;
                    UiBuilder.Stretch(bar, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), new Vector2(0f, -8f), Vector2.zero);
                }

                // ikon (üstte)
                var iconHost = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
                iconHost.SetParent(area, false);
                iconHost.anchorMin = new Vector2(0.5f, 1f);
                iconHost.anchorMax = new Vector2(0.5f, 1f);
                iconHost.pivot = new Vector2(0.5f, 1f);
                iconHost.sizeDelta = new Vector2(NavIcons.Size, NavIcons.Size);
                iconHost.anchoredPosition = new Vector2(0f, -22f);
                NavIcons.Draw(iconHost, tab, tone, BarColor);

                // yazı (altta)
                Text label = UiBuilder.CreateText(area, "Label", 30, TextAnchor.MiddleCenter, tone);
                label.text = item.Label;
                label.raycastTarget = false;
                label.fontStyle = item.IsActive ? FontStyle.Bold : FontStyle.Normal;
                UiBuilder.Stretch(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 12f), new Vector2(-4f, 56f));
                index++;
            }
        }
    }
}
