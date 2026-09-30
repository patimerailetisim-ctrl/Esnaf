using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Phone
{
    /// <summary>
    /// Telefon sektörü paketinin nitelik anahtarları ve değer kimlikleri (GDD v0.3 4.4 örneğiyle aynı adlar).
    /// Ürün örneğindeki <c>Attributes</c> torbası bu anahtarlarla doldurulur; çarpanlar value_tables.json'dadır.
    /// </summary>
    public static class PhoneAttributes
    {
        // Nitelik anahtarları
        public const string Battery = "battery";   // sayı: pil sağlığı %
        public const string Screen = "screen";     // metin: ekran durumu kimliği
        public const string Body = "body";         // sayı: kasa kozmetik %
        public const string Camera = "camera";     // metin: kamera durumu kimliği
        public const string Box = "box";           // bayrak: kutu var mı
        public const string Invoice = "invoice";   // bayrak: fatura var mı

        // Ekran durumu kimlikleri
        public const string ScreenOriginal = "original";
        public const string ScreenScratched = "scratched";
        public const string ScreenReplacedAftermarket = "replaced_aftermarket";
        public const string ScreenCracked = "cracked";

        // Kamera durumu kimlikleri
        public const string CameraOk = "ok";
        public const string CameraSpotted = "spotted";
        public const string CameraFaulty = "faulty";

        /// <summary>value_tables.json'da bulunması ZORUNLU ekran kimlikleri.</summary>
        public static readonly IReadOnlyList<string> RequiredScreenIds = new ReadOnlyCollection<string>(
            new[] { ScreenOriginal, ScreenScratched, ScreenReplacedAftermarket, ScreenCracked });

        /// <summary>value_tables.json'da bulunması ZORUNLU kamera kimlikleri.</summary>
        public static readonly IReadOnlyList<string> RequiredCameraIds = new ReadOnlyCollection<string>(
            new[] { CameraOk, CameraSpotted, CameraFaulty });
    }
}
