using System.Collections.Generic;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Kayıt klasörünün soyutlaması (GDD v0.3 6.1): yalnızca <c>Esnaf.App</c> gerçek yolu (<c>persistentDataPath/saves</c>) bilir.
    /// Ad = klasör içindeki dosya adı ("slot0.json"). Her işlem G/Ç hatasında IOException (ya da UnauthorizedAccessException) fırlatır.
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists(string name);

        string ReadAllText(string name);

        /// <summary>Dosyayı yazar, diske BOŞALTIR ve varsa üzerine yazar.</summary>
        void WriteAllText(string name, string text);

        /// <summary>Kopyalar (hedefin üzerine yazar); kaynak durur.</summary>
        void Copy(string from, string to);

        /// <summary>Kaynağı hedefin üzerine ATOMİK taşır (hedef ya eskisi ya yenisidir); kaynak kalkar.</summary>
        void Replace(string from, string to);

        /// <summary>Siler; dosya yoksa sessizce geçer.</summary>
        void Delete(string name);

        /// <summary>Klasördeki dosya adları.</summary>
        IReadOnlyList<string> List();
    }
}
