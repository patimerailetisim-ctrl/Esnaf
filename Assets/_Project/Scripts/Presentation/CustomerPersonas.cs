using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Müşterinin görünen kimliği: ad ve portre dosya adı (aynıdır). Davranışı/kişiliği belirlemez.</summary>
    public sealed class CustomerPersona
    {
        public string Name { get; }
        public string Gender { get; }

        public CustomerPersona(string name, string gender)
        {
            Name = name;
            Gender = gender;
        }
    }

    /// <summary>
    /// 28 kişilik görünen kimlik havuzu (16 erkek, 12 kadın; Art/Customers portreleriyle birebir). Kimlik YALNIZCA görünümdür:
    /// müşterinin kişiliği, bütçesi ve davranışı NPC rolünden (CustomerProfile) gelir ve ada bağlı değildir. Seçim, müşteri numarasının
    /// SAF bir fonksiyonudur (rastgelelik tüketilmez, kayıt durumu eklenmez): aynı oyun = aynı kimlikler. Cinsiyet NPC'nin içerikteki
    /// "gender" alanından gelir; erkek NPC yalnızca erkek adı/portresi, kadın NPC yalnızca kadın adı/portresi alır.
    /// Aynı cinsiyetten art arda gelen müşteri numaraları havuz boyunca farklı adlara gider (günlük 5 müşteri hiçbir zaman aynı adı almaz).
    /// </summary>
    public static class CustomerPersonas
    {
        public const string Male = "male";
        public const string Female = "female";

        /// <summary>Çarpan havuz boyutlarıyla (16 ve 12) aralarında asaldır: art arda numaralar birbirinden farklı sıraya düşer.</summary>
        private const long Step = 5L;

        public static readonly IReadOnlyList<string> MaleNames = new ReadOnlyCollection<string>(new[]
        {
            "Ahmet", "Arda", "Berk", "Burak", "Can", "Efe", "Emre", "Eren", "Kaan", "Kerem", "Mehmet", "Mert", "Murat", "Onur", "Oğuz", "Yiğit"
        });

        public static readonly IReadOnlyList<string> FemaleNames = new ReadOnlyCollection<string>(new[]
        {
            "Buse", "Ceren", "Damla", "Derya", "Ece", "Elif", "Melis", "Merve", "Selin", "Sude", "Zeynep", "İrem"
        });

        /// <summary>
        /// Müşteri numarası + NPC cinsiyetinden kimlik. Cinsiyet bilinmiyorsa (içerikte yoksa) null döner: çağıran NPC'nin kendi adını kullanır.
        /// </summary>
        public static CustomerPersona For(long customerId, string gender)
        {
            IReadOnlyList<string> pool;
            if (gender == Male)
            {
                pool = MaleNames;
            }
            else if (gender == Female)
            {
                pool = FemaleNames;
            }
            else
            {
                return null;
            }

            long index = (customerId * Step) % pool.Count;
            if (index < 0)
            {
                index += pool.Count;
            }

            return new CustomerPersona(pool[(int)index], gender);
        }
    }
}
