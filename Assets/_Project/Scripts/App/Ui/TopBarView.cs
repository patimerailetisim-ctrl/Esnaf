using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>Üst bar: solda gün, ortada günün saati, sağda nakit. Yalnızca <see cref="TopBarViewModel"/> metinlerini gösterir.</summary>
    internal sealed class TopBarView
    {
        public const float Height = 140f;

        private readonly Text _day;
        private readonly Text _cash;
        private readonly Text _clock;

        public TopBarView(Transform canvas)
        {
            RectTransform bar = UiBuilder.CreatePanel(canvas, "TopBar", new Color(0.13f, 0.15f, 0.2f, 1f));
            UiBuilder.Stretch(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -Height), Vector2.zero);

            _day = UiBuilder.CreateText(bar, "DayText", 52, TextAnchor.MiddleLeft, Color.white);
            UiBuilder.Stretch(_day.rectTransform, new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Vector2(40f, 0f), Vector2.zero);

            // Günün saati (Gün 12.5): gün ile nakit arasında.
            _clock = UiBuilder.CreateText(bar, "ClockText", 52, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f, 1f));
            UiBuilder.Stretch(_clock.rectTransform, new Vector2(0.3f, 0f), new Vector2(0.55f, 1f), Vector2.zero, Vector2.zero);

            _cash = UiBuilder.CreateText(bar, "CashText", 52, TextAnchor.MiddleRight, new Color(0.55f, 0.95f, 0.6f, 1f));
            UiBuilder.Stretch(_cash.rectTransform, new Vector2(0.55f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-40f, 0f));
        }

        public void Show(TopBarViewModel model)
        {
            _day.text = model.DayText;
            _cash.text = model.CashText;
            _clock.text = model.ClockText;
        }
    }
}
