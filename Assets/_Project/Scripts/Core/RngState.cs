namespace Esnaf.Core
{
    /// <summary>Bir PCG akışının kaydedilebilir durumu (kayıt dosyasına girer).</summary>
    public readonly struct RngState
    {
        public ulong State { get; }
        public ulong Increment { get; }

        public RngState(ulong state, ulong increment)
        {
            State = state;
            Increment = increment;
        }
    }
}
