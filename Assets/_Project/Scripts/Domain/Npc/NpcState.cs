using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// NPC DURUMU (kayda girer): görüşme sayısı ve oyuncuyla hangi ürünlerin el değiştirdiği (arbitraj engeli için, GDD 10.4).
    /// Tanımı yalnızca <see cref="NpcId"/> ile gösterir. Değişiklik yalnızca <see cref="NpcStateStore"/> üzerinden yapılır.
    /// </summary>
    public sealed class NpcState
    {
        private readonly SortedSet<long> _soldToPlayer = new SortedSet<long>();
        private readonly SortedSet<long> _boughtFromPlayer = new SortedSet<long>();

        public string NpcId { get; }

        /// <summary>Bu NPC ile yapılan görüşme (pazarlık) sayısı.</summary>
        public int EncounterCount { get; internal set; }

        /// <summary>NPC'nin oyuncuya sattığı ürün örneklerinin kimlikleri (artan sırada, salt okunur kopya).</summary>
        public IReadOnlyList<long> SoldToPlayer
        {
            get { return new ReadOnlyCollection<long>(_soldToPlayer.ToList()); }
        }

        /// <summary>NPC'nin oyuncudan aldığı ürün örneklerinin kimlikleri (artan sırada, salt okunur kopya).</summary>
        public IReadOnlyList<long> BoughtFromPlayer
        {
            get { return new ReadOnlyCollection<long>(_boughtFromPlayer.ToList()); }
        }

        public NpcState(string npcId)
        {
            NpcId = npcId;
        }

        internal void AddSoldToPlayer(long instanceId)
        {
            _soldToPlayer.Add(instanceId);
        }

        internal void AddBoughtFromPlayer(long instanceId)
        {
            _boughtFromPlayer.Add(instanceId);
        }

        internal bool HasSoldToPlayer(long instanceId)
        {
            return _soldToPlayer.Contains(instanceId);
        }

        internal bool HasBoughtFromPlayer(long instanceId)
        {
            return _boughtFromPlayer.Contains(instanceId);
        }
    }
}
