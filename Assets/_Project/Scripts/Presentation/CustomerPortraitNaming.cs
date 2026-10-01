using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Müşteri portresi dosya adı ↔ müşteri adı eşleşmesi (Unity'siz). Portre "Ahmet.png" gibi ADIN kendisidir; müşterinin görünen adından
    /// unvan/hitap sözcükleri (Dr., Abi, Abla, Teyze, Amca, Bey, Hanım) atılır, ilk sözcük alınır ve büyük/küçük harf ile Türkçe
    /// harf farkı yok sayılır: "Dr. Murat" → "murat", "Oğuz" → "oguz", "İrem" → "irem". Oyun kuralı ya da müşteri üretimi değildir.
    /// </summary>
    public static class CustomerPortraitNaming
    {
        /// <summary>Art/Customers klasöründe beklenen 28 portrenin adları (dosya adı, uzantısız).</summary>
        public static readonly IReadOnlyList<string> ExpectedNames = new ReadOnlyCollection<string>(new[]
        {
            "Ahmet", "Arda", "Berk", "Burak", "Buse", "Can", "Ceren", "Damla", "Derya", "Ece", "Efe", "Elif", "Emre", "Eren",
            "Kaan", "Kerem", "Mehmet", "Melis", "Mert", "Merve", "Murat", "Onur", "Oğuz", "Selin", "Sude", "Yiğit", "Zeynep", "İrem"
        });

        private static readonly HashSet<string> Titles = new HashSet<string>(StringComparer.Ordinal)
        {
            "dr", "abi", "abla", "teyze", "amca", "bey", "hanim", "hanım", "usta", "hoca"
        };

        /// <summary>Müşteri adı ya da portre dosya adı → karşılaştırma anahtarı; anahtar çıkmazsa boş.</summary>
        public static string Key(string nameOrFile)
        {
            if (string.IsNullOrWhiteSpace(nameOrFile))
            {
                return string.Empty;
            }

            string name = nameOrFile.Trim();
            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            foreach (string raw in name.Split(new[] { ' ', '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = Fold(raw);
                if (token.Length > 0 && !Titles.Contains(token))
                {
                    return token;
                }
            }

            return string.Empty;
        }

        private static string Fold(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case 'İ':
                    case 'I':
                    case 'ı':
                    case 'i':
                        sb.Append('i');
                        break;
                    case 'Ğ':
                    case 'ğ':
                        sb.Append('g');
                        break;
                    case 'Ü':
                    case 'ü':
                        sb.Append('u');
                        break;
                    case 'Ş':
                    case 'ş':
                        sb.Append('s');
                        break;
                    case 'Ö':
                    case 'ö':
                        sb.Append('o');
                        break;
                    case 'Ç':
                    case 'ç':
                        sb.Append('c');
                        break;
                    default:
                        sb.Append(char.ToLowerInvariant(c));
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
