using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>Profil ekranı (Gün 13.4: yer tutucu; asıl Profil Gün 13.5'te yapılacak). Yalnızca bir başlık ve not gösterir.</summary>
    internal sealed class ProfileView
    {
        private readonly UiFlow _flow;
        private readonly GameObject _root;

        public ProfileView(Transform canvas, UiFlow flow)
        {
            _flow = flow;
            RectTransform root = UiBuilder.CreatePanel(canvas, "ProfileScreen", UiTheme.Background);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, new Vector2(0f, NavBarView.Height), new Vector2(0f, -TopBarView.Height));

            Text title = UiKit.Label(root, "Title", TurkishTexts.NavProfile, 60, UiTheme.Ink, true, TextAnchor.MiddleCenter);
            UiBuilder.Stretch(title.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.7f), new Vector2(40f, 0f), new Vector2(-40f, 0f));
            Text note = UiKit.Label(root, "Note", TurkishTexts.ProfilePlaceholder, 40, UiTheme.Muted, false, TextAnchor.MiddleCenter);
            UiBuilder.Stretch(note.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 0.55f), new Vector2(40f, 0f), new Vector2(-40f, 0f));
        }

        public void Show()
        {
            _root.SetActive(_flow.CurrentScreen == UiScreen.Profile);
        }
    }
}
