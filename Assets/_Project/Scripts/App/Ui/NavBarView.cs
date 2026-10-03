using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Kalıcı alt navigasyon (Gün 13.4): Dükkan | Toptancı | İlanlar | Profil. Tüm ekranların altında sabit durur; ekranlar bu yüksekliğin üstüne yerleşir.
    /// Yalnızca <see cref="UiFlow.Nav"/>'ı çizer ve <see cref="UiFlow.GoToTab"/>'ı çağırır; oyun kuralı yoktur. Etiketler yazıdır (emoji yazı tipi bağımlılığı yok).
    /// </summary>
    internal sealed class NavBarView
    {
        public const float Height = 150f;

        private readonly UiFlow _flow;
        private readonly RectTransform _bar;

        public NavBarView(Transform canvas, UiFlow flow)
        {
            _flow = flow;
            _bar = UiKit.Rounded(canvas, "NavBar", UiTheme.Card);
            UiBuilder.Stretch(_bar, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, Height));
            UiKit.AddShadow(_bar.gameObject, 0.1f, 10f);
        }

        public void Show()
        {
            UiKit.Clear(_bar); // Play Mode'da Destroy, Edit Mode'da (testler) DestroyImmediate
            _bar.SetAsLastSibling();
            int index = 0;
            foreach (NavItemViewModel item in _flow.Nav.Items)
            {
                NavTab tab = item.Tab;
                Button button = UiKit.RoundedButton(
                    _bar, "Nav_" + tab, item.Label, 40, item.IsActive ? UiTheme.Gold : UiTheme.CardSoft, UiTheme.Ink, () => _flow.GoToTab(tab));
                float min = index * 0.25f;
                UiBuilder.Stretch(button.GetComponent<RectTransform>(), new Vector2(min, 0f), new Vector2(min + 0.25f, 1f), new Vector2(8f, 14f), new Vector2(-8f, -14f));
                button.GetComponentInChildren<Text>().fontStyle = item.IsActive ? FontStyle.Bold : FontStyle.Normal;
                index++;
            }
        }
    }
}
