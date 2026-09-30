using System;
using System.Collections.Generic;
using Esnaf.Core;

namespace Esnaf.Tests.Support
{
    /// <summary>Chance çağrılarının olasılıklarını kaydeden, hep aynı cevabı veren sahte rastgelelik (formüldeki olasılık seçimini sınar).</summary>
    public sealed class RecordingRandom : IRandom
    {
        private readonly bool _chanceAnswer;
        private readonly double _doubleAnswer;

        public List<double> ChanceProbabilities { get; } = new List<double>();
        public int DoubleCalls { get; private set; }

        public RecordingRandom(bool chanceAnswer, double doubleAnswer)
        {
            _chanceAnswer = chanceAnswer;
            _doubleAnswer = doubleAnswer;
        }

        public bool Chance(double probability)
        {
            ChanceProbabilities.Add(probability);
            return _chanceAnswer;
        }

        public double NextDouble()
        {
            DoubleCalls++;
            return _doubleAnswer;
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
