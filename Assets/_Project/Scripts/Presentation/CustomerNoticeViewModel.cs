namespace Esnaf.Presentation
{
    /// <summary>
    /// Global müşteri bildirimi (Gün 13.1): oyuncu hangi ekranda olursa olsun görünen "Müşteri geldi: Ad" ya da "Müşteri ayrıldı: Ad" kartı.
    /// Gelen müşteri için "Müşteriye Git" ve "Kapat" vardır; ayrılan müşteri için yalnızca "Kapat". Oyun durumu içermez; yalnızca <see cref="UiFlow"/> üretir.
    /// </summary>
    public sealed class CustomerNoticeViewModel
    {
        public long CustomerId { get; }
        public string NpcId { get; }
        public string Name { get; }

        /// <summary>"Müşteri geldi: Kemal Abi" / "Müşteri ayrıldı: Kemal Abi".</summary>
        public string Title { get; }

        /// <summary>Müşterinin kısa doğal cümlesi (karşılama ya da ayrılırken söylediği).</summary>
        public string Line { get; }

        /// <summary>Müşteri ayrıldı mı? Ayrıldıysa "Müşteriye Git" yoktur.</summary>
        public bool IsLeft { get; }

        public bool CanGo { get; }
        public string GoButtonText { get; }
        public string DismissButtonText { get; }

        public CustomerNoticeViewModel(long customerId, string npcId, string name, string title, string line, bool isLeft)
        {
            CustomerId = customerId;
            NpcId = npcId;
            Name = name;
            Title = title;
            Line = line;
            IsLeft = isLeft;
            CanGo = !isLeft;
            GoButtonText = TurkishTexts.GoToCustomerButton;
            DismissButtonText = TurkishTexts.NoticeDismissButton;
        }
    }
}
