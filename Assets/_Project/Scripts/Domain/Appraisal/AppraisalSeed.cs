using System.Globalization;
using System.Text;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Ekspertiz tohumu (GDD v0.3 4.5): <c>hash(masterSeed, instanceId, level)</c>. FNV-1a 64 kullanılır (platformdan bağımsız;
    /// <c>string.GetHashCode</c> kullanılmaz). Aynı ürün + aynı seviye her zaman aynı tohumu, dolayısıyla aynı sonucu verir.
    /// </summary>
    public static class AppraisalSeed
    {
        /// <summary>Ekspertiz PCG akışının sabit numarası ("APPR").</summary>
        public const ulong Stream = 0x41505052UL;

        public static ulong Compute(ulong masterSeed, long instanceId, string levelId)
        {
            string text = "appraisal|" + masterSeed.ToString(CultureInfo.InvariantCulture)
                          + "|" + instanceId.ToString(CultureInfo.InvariantCulture) + "|" + levelId;
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
