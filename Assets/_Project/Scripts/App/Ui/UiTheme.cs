using Esnaf.Presentation;
using UnityEngine;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Kârbaz görsel dili: açık gri zemin, koyu lacivert metin, altın vurgu; yeşil = olumlu, turuncu = uyarı, kırmızı = hasar.
    /// Renkler tek yerde durur; ekranlar buradan okur. Yalnızca görünüm.
    /// </summary>
    internal static class UiTheme
    {
        public static readonly Color Background = new Color(0.937f, 0.945f, 0.957f, 1f);
        public static readonly Color Card = new Color(1f, 1f, 1f, 1f);
        public static readonly Color CardSoft = new Color(0.965f, 0.973f, 0.984f, 1f);
        public static readonly Color Ink = new Color(0.067f, 0.105f, 0.2f, 1f);
        public static readonly Color Muted = new Color(0.42f, 0.47f, 0.56f, 1f);
        public static readonly Color Gold = new Color(0.96f, 0.72f, 0.16f, 1f);
        public static readonly Color GoldSoft = new Color(1f, 0.93f, 0.7f, 1f);
        public static readonly Color Good = new Color(0.16f, 0.6f, 0.36f, 1f);
        public static readonly Color GoodSoft = new Color(0.84f, 0.95f, 0.88f, 1f);
        public static readonly Color Warn = new Color(0.93f, 0.55f, 0.1f, 1f);
        public static readonly Color WarnSoft = new Color(1f, 0.91f, 0.78f, 1f);
        public static readonly Color Bad = new Color(0.8f, 0.22f, 0.2f, 1f);
        public static readonly Color BadSoft = new Color(0.99f, 0.86f, 0.85f, 1f);
        public static readonly Color Disabled = new Color(0.82f, 0.85f, 0.9f, 1f);

        public static Color ToneColor(UiTone tone)
        {
            switch (tone)
            {
                case UiTone.Good:
                    return Good;
                case UiTone.Warn:
                    return Warn;
                case UiTone.Bad:
                    return Bad;
                default:
                    return Muted;
            }
        }

        public static Color ToneSoft(UiTone tone)
        {
            switch (tone)
            {
                case UiTone.Good:
                    return GoodSoft;
                case UiTone.Warn:
                    return WarnSoft;
                case UiTone.Bad:
                    return BadSoft;
                default:
                    return CardSoft;
            }
        }
    }
}
