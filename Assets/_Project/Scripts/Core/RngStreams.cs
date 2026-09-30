using System;
using System.Collections.Generic;
using System.Text;

namespace Esnaf.Core
{
    /// <summary>
    /// Ana seed'den ADLANDIRILMIŞ bağımsız akışlar türetir ("market", "npc", "appraisal"...).
    /// Bir akıştan fazladan sayı çekmek diğer akışların gelecekteki değerlerini değiştirmez.
    /// </summary>
    public sealed class RngStreams
    {
        private readonly Dictionary<string, PcgRandom> _streams = new Dictionary<string, PcgRandom>();

        public ulong MasterSeed { get; }

        public RngStreams(ulong masterSeed)
        {
            MasterSeed = masterSeed;
        }

        public IRandom Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Stream name is required.", nameof(name));
            }

            PcgRandom stream;
            if (!_streams.TryGetValue(name, out stream))
            {
                stream = new PcgRandom(MasterSeed, Fnv1a64(name));
                _streams.Add(name, stream);
            }

            return stream;
        }

        /// <summary>Şu ana kadar oluşturulan tüm akışların durumu (kayıt için).</summary>
        public Dictionary<string, RngState> Capture()
        {
            var snapshot = new Dictionary<string, RngState>(_streams.Count);
            foreach (KeyValuePair<string, PcgRandom> pair in _streams)
            {
                snapshot.Add(pair.Key, pair.Value.GetState());
            }

            return snapshot;
        }

        /// <summary>
        /// Kayıtlı durumu yükler. Kayıtta olmayan akışlar ana seed'den sıfırdan (deterministik) türetilir.
        /// </summary>
        public void Restore(IDictionary<string, RngState> snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _streams.Clear();
            foreach (KeyValuePair<string, RngState> pair in snapshot)
            {
                _streams.Add(pair.Key, PcgRandom.FromState(pair.Value));
            }
        }

        // string.GetHashCode() platforma göre değişir; bu yüzden kendi sabit özet fonksiyonumuz.
        internal static ulong Fnv1a64(string text)
        {
            unchecked
            {
                ulong hash = 0xcbf29ce484222325UL;
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 0x100000001b3UL;
                }

                return hash;
            }
        }
    }
}
