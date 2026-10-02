using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Accessories;
using Esnaf.Domain.Business;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Game;
using Esnaf.Domain.Negotiation;
using Esnaf.Domain.Npc;

namespace Esnaf.Presentation
{
    /// <summary>Satış ekranının UI durumu (UiFlow'un elinde): konuşma aşaması, seçili fiyat, son sözler, biten satış. Oyun durumu DEĞİLDİR.</summary>
    internal sealed class SaleUiState
    {
        public int Stage;                    // 0: selamlaştı mı, 1: fiyat konuşuluyor
        public long AskTl;                   // oyuncunun seçtiği fiyat (10 ₺'nin katı, adım 100)
        public string CustomerLine;
        public string PlayerLine;
        public SaleView Final;               // biten satışın son görünümü (anlaşma / müşteri gitti)
        public string DefinitionId;          // ürünün modeli (satış bitince raftan çıkar)
        public string AddOnFeedback;         // son aksesuar ekleme sonucu (başarı ya da Türkçe hata)
        public bool AddOnFeedbackIsError;
        public bool Initialized;
    }

    /// <summary>Satış ekranının görünüm modelini IGameApi + CustomerProfile'dan kurar. Kural yoktur: yalnızca metne ve düğmelere çevirir.</summary>
    internal static class SaleScreenBuilder
    {
        public const long PriceStep = 100L;

        public static SaleScreenViewModel Build(IGameApi api, ContentPresentation content, SaleUiState ui)
        {
            SaleView sale = api.GetSale();
            if (sale != null)
            {
                return BuildTalking(api, content, ui, sale);
            }

            if (ui.Final != null)
            {
                return BuildDone(api, content, ui);
            }

            return BuildLobby(api, content);
        }

        private static SaleScreenViewModel BuildLobby(IGameApi api, ContentPresentation content)
        {
            var cards = new List<SaleCustomerCardViewModel>();
            IReadOnlyList<StockLine> stock = api.GetInventory();
            foreach (CustomerView customer in api.GetCustomers())
            {
                string model = string.Empty;
                foreach (StockLine line in stock)
                {
                    if (line.InstanceId == customer.InstanceId)
                    {
                        model = content.ModelName(line.DefinitionId);
                    }
                }

                cards.Add(new SaleCustomerCardViewModel(
                    customer.CustomerId,
                    customer.NpcId,
                    content.CustomerName(customer.CustomerId, customer.NpcId),
                    customer.Profile == null ? string.Empty : customer.Profile.PersonalityName,
                    TurkishTexts.CustomerInterest(model)));
            }

            return new SaleScreenViewModel(
                SaleMode.Lobby, TurkishTexts.CustomersTitle, cards, cards.Count == 0 ? TurkishTexts.NoCustomers : null,
                null, null, null, null, null, null, null, null, null, new SaleReplyViewModel[0], false, Money.Zero, null);
        }

        private static SaleScreenViewModel BuildTalking(IGameApi api, ContentPresentation content, SaleUiState ui, SaleView sale)
        {
            var replies = new List<SaleReplyViewModel>();
            bool stepper = false;
            Money ask = Money.FromTl(ui.AskTl);

            if (sale.Phase == NegotiationPhase.FinalOffer)
            {
                replies.Add(new SaleReplyViewModel(SaleReplyKind.AcceptFinal, TurkishTexts.ReplyAcceptFinal(sale.ShownPrice), true));
            }
            else if (ui.Stage == 0)
            {
                replies.Add(new SaleReplyViewModel(SaleReplyKind.Greet, TurkishTexts.ReplyGreet, true));
            }
            else
            {
                stepper = true;
                replies.Add(new SaleReplyViewModel(SaleReplyKind.Ask, TurkishTexts.ReplyPrice(ask), true));
                if (!sale.ReportShown && sale.Reports.Count > 0)
                {
                    replies.Add(new SaleReplyViewModel(SaleReplyKind.ShowReport, TurkishTexts.ReplyReport, false, sale.Reports[0].AppraisalId));
                }
            }

            replies.Add(new SaleReplyViewModel(SaleReplyKind.LetGo, TurkishTexts.ReplyLetGo, false));

            return Describe(SaleMode.Talking, content, ui, sale, replies, stepper, ask);
        }

        private static SaleScreenViewModel BuildDone(IGameApi api, ContentPresentation content, SaleUiState ui)
        {
            var replies = new List<SaleReplyViewModel> { new SaleReplyViewModel(SaleReplyKind.Continue, TurkishTexts.SaleDoneButton, true) };
            return Describe(SaleMode.Done, content, ui, ui.Final, replies, false, Money.Zero, BuildAddOn(api, ui));
        }

        // Aksesuar paneli yalnızca anlaşmayla biten telefon satışında ve oyun aynı satışı (aynı tutar) ek satışa açık gösteriyorsa kurulur.
        private static AddOnPanelViewModel BuildAddOn(IGameApi api, SaleUiState ui)
        {
            if (ui.Final == null || ui.Final.Phase != NegotiationPhase.Deal)
            {
                return null;
            }

            AccessoryAddOnView view = api.GetAccessoryAddOns();
            if (!view.HasPhoneSale || view.BuyerNpcId != ui.Final.NpcId || view.PhoneSalePrice != ui.Final.DealPrice)
            {
                return null;
            }

            var cards = new List<AddOnCardViewModel>();
            foreach (AccessoryAddOnOptionView option in view.Options)
            {
                cards.Add(new AddOnCardViewModel(
                    option.AccessoryId,
                    option.AccessoryName,
                    TurkishTexts.AddOnPriceLine(option.RetailPrice),
                    option.IsAvailable ? TurkishTexts.AddOnStockLine(option.InStock) : TurkishTexts.AddOnOutOfStock,
                    option.InStock,
                    option.IsAvailable ? TurkishTexts.AddOnAddButton : TurkishTexts.AddOnOutOfStock,
                    option.IsAvailable));
            }

            return new AddOnPanelViewModel(
                view.PhoneSaleRecordId, cards, view.PhoneSalePrice, view.AccessoryRevenue, view.PhoneProfit, view.AccessoryProfit,
                view.AddOnsSold, ui.AddOnFeedback, ui.AddOnFeedbackIsError);
        }

        private static SaleScreenViewModel Describe(
            SaleMode mode, ContentPresentation content, SaleUiState ui, SaleView sale, List<SaleReplyViewModel> replies, bool stepper, Money ask, AddOnPanelViewModel addOn = null)
        {
            CustomerProfile profile = sale.Profile;
            string info = profile == null ? string.Empty : profile.PersonalityName + " • " + TurkishTexts.BudgetOf(profile.Budget);
            string title = mode == SaleMode.Done && sale.Phase == NegotiationPhase.Deal ? TurkishTexts.SaleDeal(sale.DealPrice) : content.CustomerName(sale.CustomerId, sale.NpcId);

            return new SaleScreenViewModel(
                mode,
                title,
                new SaleCustomerCardViewModel[0],
                null,
                sale.NpcId,
                content.CustomerName(sale.CustomerId, sale.NpcId),
                info,
                TurkishTexts.CustomerMood(sale.Mood),
                TurkishTexts.CustomerPatience(sale.Patience),
                content.ModelName(ui.DefinitionId),
                ui.DefinitionId,
                ui.CustomerLine,
                ui.PlayerLine,
                replies,
                stepper,
                ask,
                stepper ? MoneyFormatter.Format(ask) : null,
                addOn);
        }
    }
}
