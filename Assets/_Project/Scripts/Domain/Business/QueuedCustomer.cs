using Esnaf.Domain.Npc;
using Esnaf.Domain.Time;

namespace Esnaf.Domain.Business
{
    /// <summary>Günlük kuyruktaki bir müşteri: sırası, mevcut NPC kimliği, geliş saati ve (varsa) mevcut kişilik profili. Saklanmaz; tohum ve günden yeniden türetilir.</summary>
    public sealed class QueuedCustomer
    {
        /// <summary>Günün kuyruğundaki sıra (0'dan).</summary>
        public int Index { get; }

        public string NpcId { get; }

        /// <summary>Geliş saati: gece yarısından beri dakika (açılış ≤ geliş &lt; kapanış).</summary>
        public int ArrivalMinute { get; }

        /// <summary>Mevcut kişilik sisteminden (10 arketip) müşteri profili; kişilik kataloğu yoksa null.</summary>
        public CustomerProfile Profile { get; }

        public string ArrivalText
        {
            get { return StoreHours.Format(ArrivalMinute); }
        }

        public QueuedCustomer(int index, string npcId, int arrivalMinute, CustomerProfile profile)
        {
            Index = index;
            NpcId = npcId;
            ArrivalMinute = arrivalMinute;
            Profile = profile;
        }
    }
}
