using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// Servete katkıda bulunan bir kalem (GDD v0.3 2.3 eklenti noktası). Nakit, stok ve varlıklar pozitif katkı verir;
    /// ileride borç, NEGATİF katkı veren yeni bir katkıcı olarak eklenebilir (MVP'de yazılmaz).
    /// </summary>
    public interface IWealthContributor
    {
        /// <summary>Kalemin kararlı anahtarı (örn. "wealth.cash").</summary>
        string Key { get; }

        Money GetValue();
    }
}
