using Esnaf.Domain.Negotiation;

namespace Esnaf.Domain.Npc
{
    /// <summary>Düzey eşiğinin yönü: Up = değer büyüdükçe düzey yükselir; Down = değer küçüldükçe düzey yükselir.</summary>
    public enum ScaleDirection
    {
        Up = 0,
        Down = 1
    }

    /// <summary>
    /// Bir sayıyı düşük/orta/yüksek düzeye çeviren iki eşik (npc_profiles.json "personalityScale"). Eşikler dahildir:
    /// Up: değer ≥ yüksek → yüksek; ≥ orta → orta; aksi düşük. Down: değer ≤ yüksek → yüksek; ≤ orta → orta; aksi düşük.
    /// </summary>
    public sealed class PersonalityScale
    {
        public ScaleDirection Direction { get; }
        public double Medium { get; }
        public double High { get; }

        public PersonalityScale(ScaleDirection direction, double medium, double high)
        {
            Direction = direction;
            Medium = medium;
            High = high;
        }

        public NegotiationLevel LevelOf(double value)
        {
            if (Direction == ScaleDirection.Up)
            {
                if (value >= High)
                {
                    return NegotiationLevel.High;
                }

                return value >= Medium ? NegotiationLevel.Medium : NegotiationLevel.Low;
            }

            if (value <= High)
            {
                return NegotiationLevel.High;
            }

            return value <= Medium ? NegotiationLevel.Medium : NegotiationLevel.Low;
        }
    }

    /// <summary>Dört sayısal boyutun ölçeği (sabır, mevcut görünüm eşikleriyle <see cref="NegotiationLevels.PatienceOf"/> ile okunur).</summary>
    public sealed class PersonalityScales
    {
        public PersonalityScale Urgency { get; }
        public PersonalityScale Knowledge { get; }
        public PersonalityScale Budget { get; }
        public PersonalityScale Haggling { get; }

        public PersonalityScales(PersonalityScale urgency, PersonalityScale knowledge, PersonalityScale budget, PersonalityScale haggling)
        {
            Urgency = urgency;
            Knowledge = knowledge;
            Budget = budget;
            Haggling = haggling;
        }
    }
}
