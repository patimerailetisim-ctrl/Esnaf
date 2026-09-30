using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// Önceden verilen değerleri sırayla döndüren sahte rastgelelik: formül testlerinde "rastgele" girdiyi sabitler.
    /// Beklenmeyen bir çağrı (kuyruk boş ya da desteklenmeyen tür) testi başarısız kılar.
    /// </summary>
    public sealed class ScriptedRandom : IRandom
    {
        private readonly Queue<bool> _chances;
        private readonly Queue<double> _doubles;

        public ScriptedRandom(IEnumerable<bool> chances, IEnumerable<double> doubles)
        {
            _chances = new Queue<bool>(chances);
            _doubles = new Queue<double>(doubles);
        }

        /// <summary>Henüz tüketilmemiş toplam değer sayısı.</summary>
        public int Remaining
        {
            get { return _chances.Count + _doubles.Count; }
        }

        public bool Chance(double probability)
        {
            if (_chances.Count == 0)
            {
                throw new InvalidOperationException("ScriptedRandom: beklenmeyen Chance çağrısı.");
            }

            return _chances.Dequeue();
        }

        public double NextDouble()
        {
            if (_doubles.Count == 0)
            {
                throw new InvalidOperationException("ScriptedRandom: beklenmeyen NextDouble çağrısı.");
            }

            return _doubles.Dequeue();
        }

        public uint NextUInt()
        {
            throw new NotSupportedException();
        }

        public int NextInt(int maxExclusive)
        {
            throw new NotSupportedException();
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            throw new NotSupportedException();
        }
    }
}
