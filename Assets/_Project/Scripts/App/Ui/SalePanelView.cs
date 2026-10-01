using Esnaf.Core;
using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Müşteri satış ekranı (Kârbaz görsel dili): üstte müşteri adı ve kısa bilgisi, ortada büyük portre + satılan telefonun gerçek görseli,
    /// altta altyazı tarzı doğal konuşma, en altta oyuncunun doğal cümle cevapları. Yalnızca <see cref="UiFlow"/> ile konuşur
    /// (OpenCustomers, StartSale, SaleGreet, AdjustSalePrice, SaleAsk, SaleShowReport, SaleAcceptFinal, SaleLetGo, SaleNext, Back);
    /// satış kuralı yoktur. İçerik her değişimde yeniden kurulur.
    /// </summary>
    internal sealed class SalePanelView
    {
        private const float HeaderHeight = 200f;
        private const float Gutter = 32f;
        private const float StageHeight = 700f;
        private const float ButtonHeight = 132f;

        private static readonly int[] StepDeltas = { -500, -100, 100, 500 };

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _kicker;
        private readonly Text _title;
        private readonly Text _info;
        private readonly RectTransform _content;

        public SalePanelView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "SaleScreen", UiTheme.Background);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarView.Height));

            _content = UiBuilder.CreateVerticalList(root, "Body", UiTheme.Background, 24f, Gutter);
            RectTransform body = _content.parent as RectTransform;
            UiBuilder.Stretch(body, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -HeaderHeight));

            RectTransform header = UiBuilder.CreatePanel(root, "Header", UiTheme.Background);
            UiBuilder.Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -HeaderHeight), Vector2.zero);

            Button back = UiKit.RoundedButton(header, "BackButton", TurkishTexts.BackButton, 40, UiTheme.Card, UiTheme.Ink, () => _flow.Back());
            UiKit.AddShadow(back.gameObject, 0.08f, 6f);
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, -50f), new Vector2(Gutter + 190f, 50f));

            _kicker = UiBuilder.CreateText(header, "Kicker", 36, TextAnchor.MiddleLeft, UiTheme.Muted);
            _kicker.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(_kicker.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, 40f), new Vector2(-Gutter, 84f));

            _title = UiBuilder.CreateText(header, "Title", 62, TextAnchor.MiddleLeft, UiTheme.Ink);
            _title.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, -28f), new Vector2(-Gutter, 44f));

            _info = UiBuilder.CreateText(header, "Info", 38, TextAnchor.MiddleLeft, UiTheme.Muted);
            UiBuilder.Stretch(_info.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, -80f), new Vector2(-Gutter, -26f));
        }

        public void Show()
        {
            SaleScreenViewModel screen = _flow.SaleScreen;
            bool visible = _flow.CurrentScreen == UiScreen.Sale && screen != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            UiKit.Clear(_content);
            _kicker.text = TurkishTexts.CustomersTitle;

            if (screen.Mode == SaleMode.Lobby)
            {
                _title.text = TurkishTexts.CustomersTitle;
                _kicker.text = string.Empty;
                _info.text = string.Empty;
                AddStatus();
                AddLobby(screen);
                return;
            }

            _title.text = screen.Mode == SaleMode.Done ? screen.Title : screen.CustomerName;
            _info.text = screen.InfoLine;
            AddStage(screen);
            AddSubtitle(screen);
            AddReplies(screen);
        }

        // ---------- lobi ----------

        private void AddLobby(SaleScreenViewModel screen)
        {
            if (screen.EmptyNote != null)
            {
                RectTransform note = UiKit.AutoCard(_content, "EmptyCard", UiTheme.Card, 8f, 40);
                UiKit.Label(note, "Note", screen.EmptyNote, 40, UiTheme.Muted, false, TextAnchor.MiddleCenter);
            }

            foreach (SaleCustomerCardViewModel customer in screen.Customers)
            {
                long customerId = customer.CustomerId;
                Button card = UiKit.RoundedButton(_content, "Customer_" + customerId, string.Empty, 30, UiTheme.Card, UiTheme.Ink, () => _flow.StartSale(customerId));
                UiKit.AddShadow(card.gameObject, 0.07f, 10f);
                RectTransform rect = card.GetComponent<RectTransform>();
                card.GetComponentInChildren<Text>().gameObject.SetActive(false);
                UiKit.Size2(rect, 0f, 200f);

                RectTransform portrait = UiKit.Rounded(rect, "Portrait", UiTheme.CardSoft);
                portrait.GetComponent<Image>().raycastTarget = false;
                UiBuilder.Stretch(portrait, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, -72f), new Vector2(172f, 72f));
                portrait.gameObject.AddComponent<RectMask2D>();
                var host = new GameObject("Host", typeof(RectTransform)).GetComponent<RectTransform>();
                host.SetParent(portrait, false);
                UiBuilder.Stretch(host, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                host.localScale = new Vector3(0.22f, 0.22f, 1f);
                CustomerPortraitView.Draw(host, customer.NpcId);

                Text name = UiKit.Label(rect, "Name", customer.Name, 46, UiTheme.Ink, true);
                UiBuilder.Stretch(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(204f, 8f), new Vector2(-28f, 84f));
                Text interest = UiKit.Label(rect, "Interest", customer.PersonalityName + "  •  " + customer.InterestLine, 32, UiTheme.Muted);
                UiBuilder.Stretch(interest.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(204f, -84f), new Vector2(-28f, 4f));
            }
        }

        // ---------- konuşma ----------

        private void AddStage(SaleScreenViewModel screen)
        {
            RectTransform stage = UiKit.Card(_content, "Stage", UiTheme.Card);
            UiKit.Size2(stage, 0f, StageHeight);

            // Sol: müşteri portresi.
            RectTransform portraitPanel = UiKit.Rounded(stage, "PortraitPanel", UiTheme.CardSoft);
            portraitPanel.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(portraitPanel, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(24f, 24f), new Vector2(-10f, -24f));
            portraitPanel.gameObject.AddComponent<RectMask2D>();
            var portrait = new GameObject("Portrait", typeof(RectTransform)).GetComponent<RectTransform>();
            portrait.SetParent(portraitPanel, false);
            portrait.anchorMin = new Vector2(0.5f, 0.5f);
            portrait.anchorMax = new Vector2(0.5f, 0.5f);
            portrait.sizeDelta = new Vector2(620f, 620f);
            portrait.anchoredPosition = new Vector2(0f, -20f);
            CustomerPortraitView.Draw(portrait, screen.NpcId);

            if (screen.Mode == SaleMode.Talking)
            {
                AddChip(portraitPanel, "Mood", "Keyfi: " + screen.MoodText, 0);
                AddChip(portraitPanel, "Patience", "Sabır: " + screen.PatienceText, 1);
            }

            // Sağ: satılan telefon (gerçek görsel; yoksa mock).
            RectTransform phonePanel = UiKit.Rounded(stage, "PhonePanel", UiTheme.CardSoft);
            phonePanel.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(phonePanel, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(10f, 24f), new Vector2(-24f, -24f));
            var phone = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            phone.SetParent(phonePanel, false);
            phone.anchorMin = new Vector2(0.5f, 0.5f);
            phone.anchorMax = new Vector2(0.5f, 0.5f);
            phone.sizeDelta = new Vector2(700f, 700f);
            phone.anchoredPosition = new Vector2(0f, 24f);
            phone.localScale = new Vector3(0.72f, 0.72f, 1f);
            PhoneMockView.Draw(phone, screen.DefinitionId, PhoneAngle.Front);

            Text model = UiKit.Label(phonePanel, "Model", screen.ModelTitle, 38, UiTheme.Ink, true, TextAnchor.MiddleCenter);
            UiBuilder.Stretch(model.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 12f), new Vector2(-12f, 84f));
        }

        private static void AddChip(RectTransform parent, string name, string text, int row)
        {
            Text label = UiKit.Badge(parent, name, UiTheme.Card, UiTheme.Ink, 30);
            label.text = text;
            label.fontStyle = FontStyle.Bold;
            RectTransform chip = label.transform.parent as RectTransform;
            float top = -16f - row * 70f;
            UiBuilder.Stretch(chip, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, top - 56f), new Vector2(-16f, top));
        }

        private void AddSubtitle(SaleScreenViewModel screen)
        {
            RectTransform card = UiKit.AutoCard(_content, "Subtitle", UiTheme.Ink, 10f, 36);
            UiKit.Label(card, "Speaker", screen.CustomerName.ToUpperInvariant(), 28, UiTheme.Gold, true);
            UiKit.Label(card, "Line", screen.CustomerLine, 46, Color.white);
            if (!string.IsNullOrEmpty(screen.PlayerLine))
            {
                Text echo = UiKit.Label(card, "PlayerLine", "Sen: " + screen.PlayerLine, 32, new Color(1f, 1f, 1f, 0.55f));
                echo.alignment = TextAnchor.UpperRight;
            }
        }

        private void AddReplies(SaleScreenViewModel screen)
        {
            AddStatus();

            if (screen.ShowsPriceStepper)
            {
                RectTransform stepper = UiKit.Card(_content, "Stepper", UiTheme.Card);
                UiKit.Size2(stepper, 0f, 150f);
                HorizontalLayoutGroup row = UiKit.Row(stepper, 12f, 20);
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = true;

                AddStepButton(stepper, StepDeltas[0]);
                AddStepButton(stepper, StepDeltas[1]);
                Text price = UiKit.Label(stepper, "Price", screen.AskPriceText, 52, UiTheme.Ink, true, TextAnchor.MiddleCenter);
                UiKit.Flex(price.rectTransform, 1f);
                AddStepButton(stepper, StepDeltas[2]);
                AddStepButton(stepper, StepDeltas[3]);
            }

            foreach (SaleReplyViewModel reply in screen.Replies)
            {
                SaleReplyViewModel chosen = reply;
                Color fill = reply.IsPrimary ? UiTheme.Gold : UiTheme.Card;
                Button button = UiKit.RoundedButton(_content, "Reply_" + reply.Kind, "“" + reply.Text + "”", 42, fill, UiTheme.Ink, () => OnReply(chosen));
                UiKit.AddShadow(button.gameObject, reply.IsPrimary ? 0.18f : 0.07f, reply.IsPrimary ? 8f : 6f);
                UiKit.Size2(button.GetComponent<RectTransform>(), 0f, ButtonHeight);
                if (reply.IsPrimary)
                {
                    button.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                }
            }
        }

        private void AddStepButton(RectTransform parent, int delta)
        {
            int captured = delta;
            string label = (delta > 0 ? "+" : "−") + System.Math.Abs(delta).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Button button = UiKit.RoundedButton(parent, "Step_" + delta, label, 34, UiTheme.CardSoft, UiTheme.Ink, () => _flow.AdjustSalePrice(captured));
            UiKit.Size2(button.GetComponent<RectTransform>(), 120f, 0f);
        }

        private void AddStatus()
        {
            string message = _flow.StatusMessage;
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            RectTransform card = UiKit.AutoCard(_content, "Status", UiTheme.WarnSoft, 4f, 24);
            UiKit.Label(card, "Message", message, 36, UiTheme.Ink, false, TextAnchor.MiddleCenter);
        }

        private void OnReply(SaleReplyViewModel reply)
        {
            switch (reply.Kind)
            {
                case SaleReplyKind.Greet:
                    _flow.SaleGreet();
                    break;
                case SaleReplyKind.Ask:
                    _flow.SaleAsk();
                    break;
                case SaleReplyKind.ShowReport:
                    _flow.SaleShowReport(reply.ReportId);
                    break;
                case SaleReplyKind.AcceptFinal:
                    _flow.SaleAcceptFinal();
                    break;
                case SaleReplyKind.LetGo:
                    _flow.SaleLetGo();
                    break;
                default:
                    _flow.SaleNext();
                    break;
            }
        }
    }
}
