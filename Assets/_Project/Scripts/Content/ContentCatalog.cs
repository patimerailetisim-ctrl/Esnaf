using Esnaf.Domain.Content;
using UnityEngine;

namespace Esnaf.Content
{
    /// <summary>
    /// Yalnızca Unity varlık bağlama kataloğu: içerik JSON dosyalarını (TextAsset) oyuna bağlar.
    /// OYUN VERİSİNİN KAYNAĞI DEĞİLDİR; sayısal/kural verisi JSON dosyalarındadır (GDD karar 12-13).
    /// Sonraki günlerde ID -> ikon/ses eşlemeleri de buraya eklenecek.
    /// </summary>
    [CreateAssetMenu(fileName = "ContentCatalog", menuName = "Esnaf/Content Catalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        [SerializeField]
        private TextAsset[] _contentFiles = new TextAsset[0];

        public IContentSource CreateContentSource()
        {
            return new TextAssetContentSource(_contentFiles);
        }
    }
}
