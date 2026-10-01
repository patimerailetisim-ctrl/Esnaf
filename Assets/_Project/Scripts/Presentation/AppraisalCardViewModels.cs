using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Bir görünüm öğesinin duygusal tonu (renk seçimi UI'dadır): olumlu, uyarı, kötü, nötr.</summary>
    public enum UiTone
    {
        Neutral = 0,
        Good = 1,
        Warn = 2,
        Bad = 3
    }

    /// <summary>Ekspertiz bulgusu satırı: ton (bulundu = uyarı, görünmüyor = olumlu), metin ve güven düzeyi. Hepsi gerçek bulgudan türer.</summary>
    public sealed class AppraisalFindingViewModel
    {
        public UiTone Tone { get; }
        public string Text { get; }
        public string ConfidenceText { get; }

        public AppraisalFindingViewModel(UiTone tone, string text, string confidenceText)
        {
            Tone = tone;
            Text = text;
            ConfidenceText = confidenceText;
        }
    }

    /// <summary>Bilgi kartı (Ekran, Kamera, Pil, Kasa...): başlık, değer metni ve ton. Yalnızca oyundan gelen sonuçlardan kurulur; veri yoksa kart yoktur.</summary>
    public sealed class AppraisalInfoCardViewModel
    {
        public string Title { get; }
        public string Value { get; }
        public string Note { get; }
        public UiTone Tone { get; }

        public AppraisalInfoCardViewModel(string title, string value, string note, UiTone tone)
        {
            Title = title;
            Value = value;
            Note = note;
            Tone = tone;
        }
    }

    /// <summary>Telefonun görünümleri (ileride gerçek ürün görselleri bu anahtarlara bağlanır).</summary>
    public enum PhoneAngle
    {
        Front = 0,
        Back = 1,
        Side = 2,
        TopBottom = 3,
        CameraClose = 4
    }

    internal static class ReadOnly
    {
        public static IReadOnlyList<T> List<T>(IEnumerable<T> items)
        {
            return new ReadOnlyCollection<T>(new List<T>(items ?? new T[0]));
        }
    }
}
