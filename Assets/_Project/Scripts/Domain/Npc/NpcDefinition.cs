using System;

namespace Esnaf.Domain.Npc
{
    /// <summary>
    /// NPC TANIMI (değişmez): aynı kişi hem satıcı hem müşteri olabilir (GDD v0.2 6). İçerik dosyasından gelir,
    /// kayda kopyalanmaz; kayıtlar yalnızca <see cref="Id"/> ile ona bakar. NPC'nin değişen hafızası <see cref="NpcState"/>'tedir.
    /// </summary>
    public sealed class NpcDefinition
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Kişilik anahtarı (haggler, hurried, honest...). Davranış kuralları ilgili sistemlerin verisindedir.</summary>
        public string Personality { get; }

        public NpcSellerRole Seller { get; }
        public NpcCustomerRole Customer { get; }

        /// <summary>Kişilik arketipi anahtarı (npc_profiles.json "personalities"); katalogsuz içerikte null.</summary>
        public string PersonalityId { get; }

        public NpcDefinition(string id, string name, string personality, NpcSellerRole seller, NpcCustomerRole customer, string personalityId = null)
        {
            if (seller == null)
            {
                throw new ArgumentNullException(nameof(seller));
            }

            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            Id = id;
            Name = name;
            Personality = personality;
            Seller = seller;
            Customer = customer;
            PersonalityId = personalityId;
        }
    }
}
