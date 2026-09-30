namespace Esnaf.Domain.Content
{
    /// <summary>
    /// İçerik dosyalarının METNİNİ sağlayan kaynak. Domain dosya sistemini/Unity'yi bilmez; metin kaynağı takılır:
    /// Unity'de TextAsset (Esnaf.Content), simülatörde/testlerde klasör veya bellek.
    /// </summary>
    public interface IContentSource
    {
        /// <param name="fileName">Yalnızca dosya adı, örn. "phone_models.json" (yol içermez).</param>
        /// <returns>Dosya yoksa false.</returns>
        bool TryGetText(string fileName, out string text);
    }
}
