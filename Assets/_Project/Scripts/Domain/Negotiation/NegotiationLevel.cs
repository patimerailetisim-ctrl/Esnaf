namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Oyuncuya gösterilen üç kademeli ipucu: satıcının ruh hali (güven) ve sabrı. Kesin sayılar (güven, sabır, R) ASLA gösterilmez;
    /// kademe eşikleri negotiation_rules.json'dadır.
    /// </summary>
    public enum NegotiationLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }
}
