using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// Tüm NPC'lerin değişen hafızası (GDD v0.3 4.3 "NPCState"). Bir NPC ilk kaydedildiğinde durumu oluşur.
    /// Bu gün yalnızca veri deposudur; kayıtları tutan akışlar (pazarlık, satış) sonraki günlerde eklenir.
    /// </summary>
    public sealed class NpcStateStore
    {
        private readonly Dictionary<string, NpcState> _states = new Dictionary<string, NpcState>(StringComparer.Ordinal);

        /// <summary>Durumu olan NPC'ler, kimliğe göre sıralı, salt okunur.</summary>
        public IReadOnlyList<NpcState> All
        {
            get
            {
                return new ReadOnlyCollection<NpcState>(
                    _states.Values.OrderBy(s => s.NpcId, StringComparer.Ordinal).ToList());
            }
        }

        public bool TryGet(string npcId, out NpcState state)
        {
            if (npcId == null)
            {
                state = null;
                return false;
            }

            return _states.TryGetValue(npcId, out state);
        }

        public void RecordEncounter(string npcId)
        {
            GetOrCreate(npcId).EncounterCount++;
        }

        public void RecordSoldToPlayer(string npcId, long instanceId)
        {
            RequireInstance(instanceId);
            GetOrCreate(npcId).AddSoldToPlayer(instanceId);
        }

        public void RecordBoughtFromPlayer(string npcId, long instanceId)
        {
            RequireInstance(instanceId);
            GetOrCreate(npcId).AddBoughtFromPlayer(instanceId);
        }

        public bool HasSoldToPlayer(string npcId, long instanceId)
        {
            NpcState state;
            return TryGet(npcId, out state) && state.HasSoldToPlayer(instanceId);
        }

        public bool HasBoughtFromPlayer(string npcId, long instanceId)
        {
            NpcState state;
            return TryGet(npcId, out state) && state.HasBoughtFromPlayer(instanceId);
        }

        private NpcState GetOrCreate(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                throw new ArgumentException("NPC id is required.", nameof(npcId));
            }

            NpcState state;
            if (!_states.TryGetValue(npcId, out state))
            {
                state = new NpcState(npcId);
                _states.Add(npcId, state);
            }

            return state;
        }

        private static void RequireInstance(long instanceId)
        {
            if (instanceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceId), "Instance ids start at 1.");
            }
        }
    }
}
