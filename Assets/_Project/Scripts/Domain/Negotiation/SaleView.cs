using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;
using Esnaf.Domain.Npc;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>Müşteriye gösterilebilecek bir ekspertiz raporu (S2/S3).</summary>
    public sealed class SaleReportView
    {
        public long AppraisalId { get; }
        public string LevelId { get; }

        public SaleReportView(long appraisalId, string levelId)
        {
            AppraisalId = appraisalId;
            LevelId = levelId;
        }
    }

    /// <summary>
    /// UI'ya giden, DEĞİŞMEZ satış pazarlığı görünümü (K3). Müşterinin Max'ı, güveni, sabrı ve değer hatası bu tipe ASLA eklenmez
    /// (bir test üyeleri denetler); oyuncu yalnızca ruh hali ve sabır KADEMESİNİ görür.
    /// </summary>
    public sealed class SaleView
    {
        public long CustomerId { get; }
        public string NpcId { get; }
        public long InstanceId { get; }
        public NegotiationPhase Phase { get; }
        public int Round { get; }

        /// <summary>Müşterinin şu anki teklifi.</summary>
        public Money ShownPrice { get; }

        /// <summary>Anlaşma fiyatı; yalnızca Deal aşamasında sıfırdan farklıdır.</summary>
        public Money DealPrice { get; }

        public NegotiationLevel Mood { get; }
        public NegotiationLevel Patience { get; }

        /// <summary>Son istenen fiyat "çok pahalı" bulundu mu.</summary>
        public bool LastAskTooExpensive { get; }

        public bool ReportShown { get; }

        /// <summary>Bu ürün için gösterilebilecek raporlar.</summary>
        public IReadOnlyList<SaleReportView> Reports { get; }

        /// <summary>Müşterinin kişilik profili (düzeyler); kişilik kataloğu yoksa null.</summary>
        public CustomerProfile Profile { get; }

        public SaleView(
            long customerId,
            string npcId,
            long instanceId,
            NegotiationPhase phase,
            int round,
            Money shownPrice,
            Money dealPrice,
            NegotiationLevel mood,
            NegotiationLevel patience,
            bool lastAskTooExpensive,
            bool reportShown,
            IEnumerable<SaleReportView> reports,
            CustomerProfile profile = null)
        {
            if (reports == null)
            {
                throw new ArgumentNullException(nameof(reports));
            }

            CustomerId = customerId;
            NpcId = npcId;
            InstanceId = instanceId;
            Phase = phase;
            Round = round;
            ShownPrice = shownPrice;
            DealPrice = dealPrice;
            Mood = mood;
            Patience = patience;
            LastAskTooExpensive = lastAskTooExpensive;
            ReportShown = reportShown;
            Profile = profile;
            Reports = new ReadOnlyCollection<SaleReportView>(new List<SaleReportView>(reports));
        }
    }
}
