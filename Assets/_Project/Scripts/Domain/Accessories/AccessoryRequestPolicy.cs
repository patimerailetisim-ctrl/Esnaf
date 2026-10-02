using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Esnaf.Core;

namespace Esnaf.Domain.Accessories
{
    /// <summary>
    /// Müşterinin telefon satışının ardından hangi aksesuarları İSTEDİĞİNİ (Gün 11.3.4) belirler. SAF ve DETERMİNİSTİKTİR: yalnızca bitmiş telefon satışının
    /// değişmez verisinden (alıcı NPC, gün, defter satırı kimliği, satış tutarı) türetilir. Oyunun rastgele sayı akışlarına DOKUNMAZ (kendi küçük sabit karması vardır),
    /// kayıtta durum tutmaz (kayıt yüklenince defterden aynı talep yeniden türer) ve aynı satış için her çağrıda aynı sonucu verir.
    ///
    /// Talep sayısı 0–<see cref="MaxRequests"/> arasıdır; çoğu müşteri aksesuar istemez. Talep edilen her aksesuar 1 adet sayılır ve bir talepte aynı aksesuar tekrar etmez.
    /// Talep stoktan bağımsızdır (stokta yoksa müşteri yine ister; satış stok kuralına takılır).
    /// </summary>
    public static class AccessoryRequestPolicy
    {
        /// <summary>Bir müşterinin tek satışta isteyebileceği en çok aksesuar sayısı.</summary>
        public const int MaxRequests = 5;

        // Talep sayısı olasılığı (yüzde, birikimli): 0 → %45, 1 → %25, 2 → %15, 3 → %8, 4 → %5, 5 → %2.
        private static readonly int[] CumulativePercent = { 45, 70, 85, 93, 98, 100 };

        private static readonly IReadOnlyList<string> None = new ReadOnlyCollection<string>(new string[0]);

        public static IReadOnlyList<string> Generate(
            string buyerNpcId, int day, long phoneSaleRecordId, Money salePrice, IReadOnlyList<AccessoryDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
            {
                return None;
            }

            ulong state = Hash(
                "addon-request|" + (buyerNpcId ?? string.Empty) + "|" + day.ToString(CultureInfo.InvariantCulture) + "|"
                + phoneSaleRecordId.ToString(CultureInfo.InvariantCulture) + "|" + salePrice.Tl.ToString(CultureInfo.InvariantCulture));

            int roll = (int)(Next(ref state) % 100UL);
            int count = 0;
            while (count < CumulativePercent.Length - 1 && roll >= CumulativePercent[count])
            {
                count++;
            }

            count = System.Math.Min(System.Math.Min(count, MaxRequests), definitions.Count);
            if (count == 0)
            {
                return None;
            }

            // Karıştırılmış sıradan ilk 'count' aksesuar (aynı aksesuar bir talepte tekrarlanmaz).
            var order = new List<int>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                order.Add(i);
            }

            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = (int)(Next(ref state) % (ulong)(i + 1));
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            var ids = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                ids.Add(definitions[order[i]].Id);
            }

            return new ReadOnlyCollection<string>(ids);
        }

        // FNV-1a (64 bit) üstüne UTF-8: platformdan bağımsız, sabit.
        private static ulong Hash(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        // SplitMix64 adımı: yerel durum, oyunun RNG'siyle ilgisi yok.
        private static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
