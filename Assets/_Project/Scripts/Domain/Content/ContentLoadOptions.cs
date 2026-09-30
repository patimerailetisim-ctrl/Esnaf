using System.Collections.Generic;

namespace Esnaf.Domain.Content
{
    public sealed class ContentLoadOptions
    {
        /// <summary>true ise content_id_manifest.json zorunludur ve ID karşılaştırması yapılır (oyun ve simülatör için).</summary>
        public bool RequireManifest { get; set; } = true;

        /// <summary>İçerikte kullanılabilecek sektör önekleri. MVP'de yalnızca "phone".</summary>
        public ICollection<string> AllowedSectors { get; } = new List<string> { "phone" };
    }
}
