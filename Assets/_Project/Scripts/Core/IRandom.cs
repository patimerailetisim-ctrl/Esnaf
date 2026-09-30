namespace Esnaf.Core
{
    /// <summary>
    /// Oyunun tek rastgelelik kapısı. UnityEngine.Random ve System.Random kural kodunda YASAKTIR (GDD K9).
    /// </summary>
    public interface IRandom
    {
        /// <summary>0 .. uint.MaxValue arası, düzgün dağılımlı 32 bit.</summary>
        uint NextUInt();

        /// <summary>[0, maxExclusive). maxExclusive &gt; 0 olmalı.</summary>
        int NextInt(int maxExclusive);

        /// <summary>[minInclusive, maxExclusive). minInclusive &lt; maxExclusive olmalı.</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>[0, 1) aralığında 53 bit hassasiyetli sayı.</summary>
        double NextDouble();

        /// <summary>
        /// Olasılıkla true döner. Sonuç ne olursa olsun HER ZAMAN aynı miktarda rastgelelik tüketir
        /// (akışların kayması önlenir). probability &lt;= 0 asla, &gt;= 1 her zaman true.
        /// </summary>
        bool Chance(double probability);
    }
}
