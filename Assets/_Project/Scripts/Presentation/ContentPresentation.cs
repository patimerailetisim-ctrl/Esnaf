using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Content;
using Esnaf.Domain.Npc;
using Esnaf.Domain.Products;

namespace Esnaf.Presentation
{
    /// <summary>Ekspertiz seviyesinin ekranda gösterilen bilgisi.</summary>
    public sealed class AppraisalLevelInfo
    {
        public string Id { get; }
        public string Name { get; }
        public int UnlockDay { get; }
        public bool RequiresEquipment { get; }

        public AppraisalLevelInfo(string id, string name, int unlockDay, bool requiresEquipment)
        {
            Id = id;
            Name = name;
            UnlockDay = unlockDay;
            RequiresEquipment = requiresEquipment;
        }
    }

    /// <summary>
    /// İçeriğin (JSON) arayüze dönük salt okunur görünümü: görünen adlar, ekspertiz seviyeleri/ücretleri, raf kapasitesi.
    /// Veri kaynağı JSON'dur; burada kural yoktur, yalnızca arama. Bulunamayan ad için kimlik gösterilir (hata yutulmaz, ekran çökmez).
    /// </summary>
    public sealed class ContentPresentation
    {
        private readonly ContentDatabase _content;
        private readonly ReadOnlyCollection<AppraisalLevelInfo> _levels;

        public ContentPresentation(ContentDatabase content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            _content = content;
            var levels = new List<AppraisalLevelInfo>();
            foreach (AppraisalLevel level in content.Appraisal.Levels)
            {
                levels.Add(new AppraisalLevelInfo(level.Id, level.Name, level.UnlockDay, level.RequiredEquipment != null));
            }

            _levels = new ReadOnlyCollection<AppraisalLevelInfo>(levels);
        }

        public int ShelfCapacity
        {
            get { return _content.EconomyConstants.InitialShelfCapacity; }
        }

        public IReadOnlyList<AppraisalLevelInfo> AppraisalLevels
        {
            get { return _levels; }
        }

        public string ModelName(string definitionId)
        {
            if (definitionId == null)
            {
                return string.Empty;
            }

            ProductDefinition product;
            return _content.TryGetProduct(definitionId, out product) ? product.Name : definitionId;
        }

        public string NpcName(string npcId)
        {
            if (npcId == null)
            {
                return string.Empty;
            }

            NpcDefinition npc;
            return _content.TryGetNpc(npcId, out npc) ? npc.Name : npcId;
        }

        /// <summary>
        /// Müşterinin GÖRÜNEN adı: NPC'nin cinsiyeti içerikte varsa 28'lik havuzdan müşteri numarasına göre bir ad (<see cref="CustomerPersonas"/>),
        /// yoksa NPC'nin kendi adı. Kişilik/davranış değişmez.
        /// </summary>
        public string CustomerName(long customerId, string npcId)
        {
            NpcDefinition npc;
            if (npcId != null && _content.TryGetNpc(npcId, out npc))
            {
                CustomerPersona persona = CustomerPersonas.For(customerId, npc.Gender);
                if (persona != null)
                {
                    return persona.Name;
                }
            }

            return NpcName(npcId);
        }

        /// <summary>Seviye ve ürün segmentine göre ekspertiz ücreti (gösterim için; ödemeyi oyun alır). Bilinmeyen seviye/ürün için false.</summary>
        public bool TryGetFee(string levelId, string definitionId, out Money fee)
        {
            fee = Money.Zero;
            if (levelId == null)
            {
                return false;
            }

            if (definitionId == null)
            {
                return false;
            }

            AppraisalLevel level;
            ProductDefinition product;
            if (!_content.Appraisal.TryGetLevel(levelId, out level) || !_content.TryGetProduct(definitionId, out product))
            {
                return false;
            }

            fee = level.FeeFor(product.Segment);
            return true;
        }
    }
}
