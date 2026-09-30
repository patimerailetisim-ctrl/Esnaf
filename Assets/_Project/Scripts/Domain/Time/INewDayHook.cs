namespace Esnaf.Domain.Time
{
    /// <summary>
    /// Yeni gün açılırken (gün sonu adım 7, ilanlar üretildikten ve gün ilerledikten SONRA; ilk gün için de) çağrılan sistemler.
    /// Ör. günlük müşteri havuzu. Kancalar başarısız olmaz; gün açılışını bozamazlar.
    /// </summary>
    public interface INewDayHook
    {
        void OnDayOpened(int day);
    }
}
