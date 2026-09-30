using System;

namespace Esnaf.Core
{
    /// <summary>
    /// PCG32 (XSH-RR). Platformdan bağımsız, seed'li ve durumu kaydedilebilir rastgele sayı üreteci.
    /// (seed 42, stream 54) için ilk çıktı 0xa15c02b7 (PCG referans uygulamasıyla aynı).
    /// </summary>
    public sealed class PcgRandom : IRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private ulong _increment;

        public PcgRandom(ulong seed, ulong stream)
        {
            Reseed(seed, stream);
        }

        public static PcgRandom FromState(RngState state)
        {
            var rng = new PcgRandom(0UL, 0UL);
            rng.SetState(state);
            return rng;
        }

        public RngState GetState()
        {
            return new RngState(_state, _increment);
        }

        public void SetState(RngState state)
        {
            if ((state.Increment & 1UL) == 0UL)
            {
                throw new ArgumentException("PCG increment must be odd.", nameof(state));
            }

            _state = state.State;
            _increment = state.Increment;
        }

        private void Reseed(ulong seed, ulong stream)
        {
            unchecked
            {
                _state = 0UL;
                _increment = (stream << 1) | 1UL;
                NextUInt();
                _state += seed;
                NextUInt();
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * Multiplier + _increment;
                uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
                int rotation = (int)(old >> 59);
                return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
            }
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be positive.");
            }

            return (int)NextBounded((uint)maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minInclusive), "minInclusive must be less than maxExclusive.");
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + NextBounded(range));
        }

        public double NextDouble()
        {
            uint high = NextUInt() >> 5;  // 27 bit
            uint low = NextUInt() >> 6;   // 26 bit
            return (high * 67108864.0 + low) / 9007199254740992.0;
        }

        public bool Chance(double probability)
        {
            if (double.IsNaN(probability))
            {
                throw new ArgumentException("Probability cannot be NaN.", nameof(probability));
            }

            return NextDouble() < probability;
        }

        // Modulo yanlılığını önleyen reddetme yöntemi.
        private uint NextBounded(uint bound)
        {
            uint threshold = (uint)(0x100000000UL % bound);
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return value % bound;
                }
            }
        }
    }
}
