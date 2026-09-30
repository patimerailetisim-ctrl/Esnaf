using Newtonsoft.Json.Linq;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Kayıt biçimi sürümünü bir yukarı taşıyan adım (GDD v0.3 6.6). Kayıt önce <c>JObject</c> olarak okunur, sürüm sürüm dönüştürülür,
    /// sonra tipli nesneye çevrilir.
    /// </summary>
    public interface IMigration
    {
        int From { get; }

        int To { get; }

        void Apply(JObject payload);
    }
}
