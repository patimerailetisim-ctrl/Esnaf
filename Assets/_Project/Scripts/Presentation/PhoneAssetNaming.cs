using System;

namespace Esnaf.Presentation
{
    /// <summary>Telefon görseli dosya adının türü. AllViews bir açı DEĞİLDİR (yalnızca referans görsel).</summary>
    public enum PhoneAssetKind
    {
        Unknown = 0,
        Front = 1,
        Back = 2,
        Side = 3,
        Camera = 4,
        AllViews = 5
    }

    /// <summary>
    /// Dosya adı sözleşmesi: "&lt;ModelAdı&gt;_&lt;Tür&gt;.png" (ör. NovaN1Lite_Front.png). Model anahtarı büyük/küçük harf ve "_" yok sayılarak içerik kimliğiyle
    /// eşleşir ("phone.nova_n1_lite" → "novan1lite"). Unity'yi bilmez; Editor kurulumu ve testler aynı kuralı kullanır.
    /// </summary>
    public static class PhoneAssetNaming
    {
        /// <summary>"phone.nova_n1_lite" → "novan1lite".</summary>
        public static string ModelKey(string definitionId)
        {
            if (definitionId == null)
            {
                return string.Empty;
            }

            const string prefix = "phone.";
            string name = definitionId.StartsWith(prefix, StringComparison.Ordinal) ? definitionId.Substring(prefix.Length) : definitionId;
            return Normalize(name);
        }

        /// <summary>
        /// "NovaN1Lite_Front.png" → anahtar "novan1lite", tür Front. Dosya adında model öneki yoksa ("Front.png") <paramref name="folderName"/> anahtar olur.
        /// Tanınmayan tür için Unknown döner.
        /// </summary>
        public static PhoneAssetKind Parse(string fileName, string folderName, out string modelKey)
        {
            modelKey = string.Empty;
            if (string.IsNullOrEmpty(fileName))
            {
                return PhoneAssetKind.Unknown;
            }

            string name = fileName;
            int dot = name.LastIndexOf('.');
            if (dot >= 0)
            {
                name = name.Substring(0, dot);
            }

            int underscore = name.LastIndexOf('_');
            string suffix = underscore >= 0 ? name.Substring(underscore + 1) : name;
            string prefix = underscore >= 0 ? name.Substring(0, underscore) : folderName;
            modelKey = Normalize(prefix);

            switch (Normalize(suffix))
            {
                case "front":
                    return PhoneAssetKind.Front;
                case "back":
                    return PhoneAssetKind.Back;
                case "side":
                    return PhoneAssetKind.Side;
                case "camera":
                    return PhoneAssetKind.Camera;
                case "allviews":
                    return PhoneAssetKind.AllViews;
                default:
                    return PhoneAssetKind.Unknown;
            }
        }

        private static string Normalize(string text)
        {
            return (text ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }
    }
}
