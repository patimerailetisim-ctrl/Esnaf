namespace Esnaf.Domain.Business
{
    /// <summary>
    /// Günlük kuyruk müşterilerinin kimlik üretimi (Gün 12.3): kimlik gün ve sıradan TÜRETİLİR (sayaç ve kayıt gerekmez) ve yuva kimliklerinden (kimlik üretecinden gelen küçük
    /// sayılar) ayrı, çok büyük bir aralıktadır. Formül yalnızca BURADA yaşar; başka hiçbir yer kimliği elle üretmez ya da çözmez.
    /// </summary>
    public static class QueueCustomerId
    {
        /// <summary>Kuyruk kimliklerinin başlangıcı; yuva kimlikleri (1, 2, 3, …) buna hiçbir zaman ulaşmaz.</summary>
        public const long Base = 1000000000L;

        /// <summary>Bir günde en çok bu kadar kuyruk müşterisi olabilir (formülün kapasitesi; günlük sayı 12'yi geçmez).</summary>
        public const int MaxPerDay = 100;

        public static long For(int day, int index)
        {
            return Base + (long)day * MaxPerDay + index;
        }

        public static bool IsQueueId(long customerId)
        {
            return customerId >= Base;
        }

        /// <summary><see cref="For"/>'un tersi; kuyruk kimliği değilse false.</summary>
        public static bool TryDecode(long customerId, out int day, out int index)
        {
            day = 0;
            index = 0;
            if (!IsQueueId(customerId))
            {
                return false;
            }

            long offset = customerId - Base;
            long d = offset / MaxPerDay;
            if (d < 1 || d > int.MaxValue)
            {
                return false;
            }

            day = (int)d;
            index = (int)(offset % MaxPerDay);
            return true;
        }
    }
}
