using System.Collections.Generic;
using System.Collections.ObjectModel;
using Esnaf.Core;

namespace Esnaf.Presentation
{
    /// <summary>Müşteri satış ekranının hali: dükkândaki müşteriler, konuşma ya da bitmiş satış.</summary>
    public enum SaleMode
    {
        Lobby = 0,
        Talking = 1,
        Done = 2
    }

    /// <summary>Oyuncunun cevap düğmesinin türü (UI hangi UiFlow komutunu çağıracağını buradan bilir).</summary>
    public enum SaleReplyKind
    {
        Greet = 0,
        Ask = 1,
        ShowReport = 2,
        AcceptFinal = 3,
        LetGo = 4,
        Continue = 5
    }

    /// <summary>Doğal cümle olarak yazılmış oyuncu cevabı. <see cref="ReportId"/> yalnızca ShowReport için anlamlıdır.</summary>
    public sealed class SaleReplyViewModel
    {
        public SaleReplyKind Kind { get; }
        public string Text { get; }
        public bool IsPrimary { get; }
        public long ReportId { get; }

        public SaleReplyViewModel(SaleReplyKind kind, string text, bool isPrimary, long reportId = 0)
        {
            Kind = kind;
            Text = text;
            IsPrimary = isPrimary;
            ReportId = reportId;
        }
    }

    /// <summary>Müşterinin istediği bir aksesuarın kartı: ad, fiyat, stok ve "Ekle" düğmesi. Eklendiyse "Eklendi" (düğme yok); stok yoksa "Stokta yok" (düğme devre dışı). Bilgiler API'den gelir.</summary>
    public sealed class AddOnCardViewModel
    {
        public string AccessoryId { get; }
        public string Name { get; }
        public string PriceLine { get; }
        public string StockLine { get; }
        public int InStock { get; }
        public string ButtonText { get; }
        public bool IsButtonEnabled { get; }

        /// <summary>Bu istek bu satışa eklendi (kartta artık Ekle düğmesi gösterilmez).</summary>
        public bool IsAdded { get; }

        public AddOnCardViewModel(
            string accessoryId, string name, string priceLine, string stockLine, int inStock, string buttonText, bool isButtonEnabled, bool isAdded = false)
        {
            AccessoryId = accessoryId;
            Name = name;
            PriceLine = priceLine;
            StockLine = stockLine;
            InStock = inStock;
            ButtonText = buttonText;
            IsButtonEnabled = isButtonEnabled;
            IsAdded = isAdded;
        }
    }

    /// <summary>
    /// Müşteri aksesuar isteyince telefon satışından sonra açılan panel: müşterinin gerçek talebi (söz), yalnızca istenen aksesuarların kartları, ilerleme (1/4),
    /// toplam/kâr özeti ve son geri bildirim. Kural içermez; hepsi IGameApi.GetAccessoryAddOns'tan gelir.
    /// </summary>
    public sealed class AddOnPanelViewModel
    {
        public string Title { get; }

        /// <summary>Müşterinin talep sözü: "Telefon tamam abi. Bir de kılıf var mı?"</summary>
        public string RequestLine { get; }

        public string CustomerName { get; }
        public long PhoneSaleRecordId { get; }
        public IReadOnlyList<AddOnCardViewModel> Cards { get; }

        /// <summary>Müşterinin istediği aksesuar sayısı (1–5) ve bunlardan eklenenler; ilerleme "eklenen/istenen".</summary>
        public int RequestedCount { get; }

        public int AddedCount { get; }
        public string ProgressLine { get; }

        public Money PhonePrice { get; }
        public Money AccessoriesPrice { get; }
        public Money TotalPrice { get; }
        public Money PhoneProfit { get; }
        public Money AccessoryProfit { get; }
        public Money TotalProfit { get; }
        public int AddOnsSold { get; }

        public string PhoneLine { get; }
        public string AccessoriesLine { get; }
        public string TotalLine { get; }
        public string PhoneProfitLine { get; }
        public string AccessoryProfitLine { get; }
        public string TotalProfitLine { get; }

        /// <summary>Son ekleme sonucu ("Şarj Kablosu eklendi (+120 ₺)." ya da hata); yoksa null.</summary>
        public string Feedback { get; }

        public bool FeedbackIsError { get; }

        /// <summary>Hiç eklenmediyse "İstemiyorum / Devam Et", en az biri eklendiyse "Devam Et".</summary>
        public string FinishButtonText { get; }

        public AddOnPanelViewModel(
            long phoneSaleRecordId, string customerName, string requestLine, IEnumerable<AddOnCardViewModel> cards,
            Money phonePrice, Money accessoriesPrice, Money phoneProfit, Money accessoryProfit, int addOnsSold,
            string feedback, bool feedbackIsError)
        {
            Title = TurkishTexts.AddOnTitle;
            CustomerName = customerName;
            RequestLine = requestLine;
            PhoneSaleRecordId = phoneSaleRecordId;
            Cards = new ReadOnlyCollection<AddOnCardViewModel>(new List<AddOnCardViewModel>(cards));
            RequestedCount = Cards.Count;
            int added = 0;
            foreach (AddOnCardViewModel card in Cards)
            {
                added += card.IsAdded ? 1 : 0;
            }

            AddedCount = added;
            ProgressLine = TurkishTexts.AddOnProgress(added, Cards.Count);
            PhonePrice = phonePrice;
            AccessoriesPrice = accessoriesPrice;
            TotalPrice = phonePrice + accessoriesPrice;
            PhoneProfit = phoneProfit;
            AccessoryProfit = accessoryProfit;
            TotalProfit = phoneProfit + accessoryProfit;
            AddOnsSold = addOnsSold;
            PhoneLine = TurkishTexts.AddOnPhoneLabel + ": " + MoneyFormatter.Format(phonePrice);
            AccessoriesLine = TurkishTexts.AddOnAccessoriesLabel + ": " + MoneyFormatter.Format(accessoriesPrice);
            TotalLine = TurkishTexts.AddOnTotalLabel + ": " + MoneyFormatter.Format(TotalPrice);
            PhoneProfitLine = TurkishTexts.AddOnPhoneProfitLabel + ": " + MoneyFormatter.Format(phoneProfit);
            AccessoryProfitLine = TurkishTexts.AddOnAccessoryProfitLabel + ": " + MoneyFormatter.Format(accessoryProfit);
            TotalProfitLine = TurkishTexts.AddOnTotalProfitLabel + ": " + MoneyFormatter.Format(TotalProfit);
            Feedback = feedback;
            FeedbackIsError = feedbackIsError;
            FinishButtonText = added == 0 ? TurkishTexts.AddOnDeclineButton : TurkishTexts.AddOnContinueButton;
        }
    }

    /// <summary>Günlük müşteri akışının satış ekranı lobisindeki durumu (Gün 12.5).</summary>
    public enum QueueLobbyState
    {
        /// <summary>Sıradaki müşterinin geliş saati gelmedi ("Sıradaki müşteri: HH:mm", Bekle).</summary>
        Waiting = 0,

        /// <summary>Aktif müşteri geldi ve ilgilendiği ürün var (kartına dokununca satış başlar).</summary>
        Arrived = 1,

        /// <summary>Aktif müşteri geldi ama ilgilendiği ürün yok (Gönder).</summary>
        NoInterest = 2,

        /// <summary>Bugünün müşterileri bitti.</summary>
        Done = 3,

        /// <summary>Mağaza kapalı (21:00).</summary>
        Closed = 4
    }

    /// <summary>
    /// Günlük müşteri kuyruğunun lobi görünümü (Gün 12.5): yalnızca ŞU AN AKTİF müşteri (en çok 1 kart), saat, ilerleme ve Bekle/Gönder eylemleri.
    /// IGameApi.GetClock / GetCustomerQueue / GetActiveCustomer'dan kurulur; kural içermez.
    /// </summary>
    public sealed class QueueLobbyViewModel
    {
        public QueueLobbyState State { get; }
        public string ClockText { get; }

        /// <summary>"Bugün: 3/10 müşteri"</summary>
        public string ProgressLine { get; }

        /// <summary>Duruma göre kısa açıklama ("Sıradaki müşteri: 10:35", "Mağaza kapandı…"); aktif müşteri varken null.</summary>
        public string StatusLine { get; }

        /// <summary>Aktif müşteri (kimlik, NPC, ad, kişilik, ilgilendiği ürün); yoksa null.</summary>
        public SaleCustomerCardViewModel Customer { get; }

        /// <summary>Sıradaki müşterinin geliş saati ("10:35"); beklemiyorsa null.</summary>
        public string NextArrivalText { get; }

        /// <summary>Bekle düğmesi görünür/kullanılabilir mi (yalnızca <see cref="QueueLobbyState.Waiting"/>).</summary>
        public bool CanWait { get; }

        /// <summary>Gönder düğmesi (yalnızca <see cref="QueueLobbyState.NoInterest"/>).</summary>
        public bool CanDismiss { get; }

        public string WaitButtonText { get; }
        public string DismissButtonText { get; }

        public QueueLobbyViewModel(
            QueueLobbyState state, string clockText, string progressLine, string statusLine, SaleCustomerCardViewModel customer, string nextArrivalText)
        {
            State = state;
            ClockText = clockText;
            ProgressLine = progressLine;
            StatusLine = statusLine;
            Customer = customer;
            NextArrivalText = nextArrivalText;
            CanWait = state == QueueLobbyState.Waiting;
            CanDismiss = state == QueueLobbyState.NoInterest;
            WaitButtonText = TurkishTexts.WaitButton;
            DismissButtonText = TurkishTexts.DismissButton;
        }
    }

    /// <summary>Dükkândaki bir müşterinin kartı (lobi).</summary>
    public sealed class SaleCustomerCardViewModel
    {
        public long CustomerId { get; }
        public string NpcId { get; }
        public string Name { get; }
        public string PersonalityName { get; }
        public string InterestLine { get; }

        public SaleCustomerCardViewModel(long customerId, string npcId, string name, string personalityName, string interestLine)
        {
            CustomerId = customerId;
            NpcId = npcId;
            Name = name;
            PersonalityName = personalityName;
            InterestLine = interestLine;
        }
    }

    /// <summary>
    /// Müşteri satış ekranı: gerçek CustomerProfile + SaleView + IGameApi'den kurulur, kural içermez. Müşterinin Max'ı, güven/sabır SAYISI yoktur;
    /// yalnızca düzeyler ve doğal konuşma metni vardır.
    /// </summary>
    public sealed class SaleScreenViewModel
    {
        public SaleMode Mode { get; }
        public string Title { get; }

        // lobi
        public IReadOnlyList<SaleCustomerCardViewModel> Customers { get; }
        public string EmptyNote { get; }

        // konuşma / bitiş
        public string NpcId { get; }
        public string CustomerName { get; }
        public string InfoLine { get; }
        public string MoodText { get; }
        public string PatienceText { get; }
        public string ModelTitle { get; }
        public string DefinitionId { get; }

        /// <summary>Müşterinin şu anki sözü (altyazı); oyuncunun son cevabı ayrıca <see cref="PlayerLine"/>'dadır.</summary>
        public string CustomerLine { get; }

        public string PlayerLine { get; }
        public IReadOnlyList<SaleReplyViewModel> Replies { get; }

        /// <summary>Fiyat seçici görünsün mü (fiyat cevabı verilebilen aşama) ve seçili fiyat.</summary>
        public bool ShowsPriceStepper { get; }

        public Money AskPrice { get; }
        public string AskPriceText { get; }

        /// <summary>
        /// Lobi modunda günlük müşteri kuyruğunun durumu (Gün 12.5); ana arayüz müşteri kartlarını BUNDAN çizer. Eski 5-yuva kartları (<see cref="Customers"/>) görünüm modelinde korunur
        /// ama ana arayüzde gösterilmez. Lobi dışında null.
        /// </summary>
        public QueueLobbyViewModel Queue { get; }

        /// <summary>Anlaşmayla biten telefon satışından sonra aksesuar paneli; başka her durumda null.</summary>
        public AddOnPanelViewModel AddOn { get; }

        public SaleScreenViewModel(
            SaleMode mode,
            string title,
            IEnumerable<SaleCustomerCardViewModel> customers,
            string emptyNote,
            string npcId,
            string customerName,
            string infoLine,
            string moodText,
            string patienceText,
            string modelTitle,
            string definitionId,
            string customerLine,
            string playerLine,
            IEnumerable<SaleReplyViewModel> replies,
            bool showsPriceStepper,
            Money askPrice,
            string askPriceText,
            AddOnPanelViewModel addOn = null,
            QueueLobbyViewModel queue = null)
        {
            Mode = mode;
            Title = title;
            Customers = new ReadOnlyCollection<SaleCustomerCardViewModel>(new List<SaleCustomerCardViewModel>(customers));
            EmptyNote = emptyNote;
            NpcId = npcId;
            CustomerName = customerName;
            InfoLine = infoLine;
            MoodText = moodText;
            PatienceText = patienceText;
            ModelTitle = modelTitle;
            DefinitionId = definitionId;
            CustomerLine = customerLine;
            PlayerLine = playerLine;
            Replies = new ReadOnlyCollection<SaleReplyViewModel>(new List<SaleReplyViewModel>(replies));
            ShowsPriceStepper = showsPriceStepper;
            AskPrice = askPrice;
            AskPriceText = askPriceText;
            AddOn = addOn;
            Queue = queue;
        }
    }
}
