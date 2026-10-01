using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Ekspertiz ekranı (Kârbaz görsel dili): üst bar (geri, model, depolama • yaş), seviye seçimi, büyük telefon alanı (yer tutucu; ileride gerçek görsel),
    /// tahmini piyasa değeri, bilgi kartları, bulgular, risk kartı ve altın ana düğme. Yalnızca <see cref="UiFlow"/> ile konuşur
    /// (AppraisalScreen, SelectLevel, PerformAppraisal, Back); ekspertiz kuralı yoktur: ücret, kilit, sonuç ve metinler IGameApi/ViewModel'den gelir.
    /// İçerik her değişimde yeniden kurulur. Sahte oyun verisi yoktur: veri olmayan kart gösterilmez.
    /// </summary>
    internal sealed class AppraisalPanelView
    {
        private const float HeaderHeight = 200f;
        private const float ActionBarHeight = 290f;
        private const float Gutter = 32f;
        private const float HeroHeight = 860f;
        private const float PhoneHeight = 700f;

        private static readonly PhoneAngle[] Angles =
        {
            PhoneAngle.Front, PhoneAngle.Back, PhoneAngle.Side, PhoneAngle.TopBottom, PhoneAngle.CameraClose
        };

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _kicker;
        private readonly Text _model;
        private readonly Text _subtitle;
        private readonly RectTransform _content;
        private readonly Text _status;
        private readonly Text _actionLabel;
        private PhoneAngle _angle = PhoneAngle.Front;

        public AppraisalPanelView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "AppraisalScreen", UiTheme.Background);
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarView.Height));

            // Gövde (kaydırılır): başlık ile eylem çubuğu arasında.
            _content = UiBuilder.CreateVerticalList(root, "Body", UiTheme.Background, 24f, Gutter);
            RectTransform body = _content.parent as RectTransform;
            UiBuilder.Stretch(body, Vector2.zero, Vector2.one, new Vector2(0f, ActionBarHeight), new Vector2(0f, -HeaderHeight));

            // Üst bar.
            RectTransform header = UiBuilder.CreatePanel(root, "Header", UiTheme.Background);
            UiBuilder.Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -HeaderHeight), Vector2.zero);

            Button back = UiKit.RoundedButton(header, "BackButton", TurkishTexts.BackButton, 40, UiTheme.Card, UiTheme.Ink, OnBackClicked);
            UiKit.AddShadow(back.gameObject, 0.08f, 6f);
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Gutter, -50f), new Vector2(Gutter + 190f, 50f));

            _kicker = UiBuilder.CreateText(header, "Kicker", 38, TextAnchor.MiddleLeft, UiTheme.Muted);
            _kicker.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(_kicker.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, 36f), new Vector2(-Gutter, 80f));

            _model = UiBuilder.CreateText(header, "Model", 64, TextAnchor.MiddleLeft, UiTheme.Ink);
            _model.fontStyle = FontStyle.Bold;
            UiBuilder.Stretch(_model.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, -28f), new Vector2(-Gutter, 40f));

            _subtitle = UiBuilder.CreateText(header, "Subtitle", 40, TextAnchor.MiddleLeft, UiTheme.Muted);
            UiBuilder.Stretch(_subtitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(Gutter + 230f, -78f), new Vector2(-Gutter, -26f));

            // Alt eylem çubuğu.
            RectTransform bar = UiKit.Rounded(root, "ActionBar", UiTheme.Card);
            UiKit.AddShadow(bar.gameObject, 0.1f, -10f);
            UiBuilder.Stretch(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, ActionBarHeight));

            _status = UiBuilder.CreateWrappedText(bar, "Status", 36, TextAnchor.MiddleCenter, UiTheme.Warn);
            UiBuilder.Stretch(_status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Gutter, -100f), new Vector2(-Gutter, -14f));

            Button action = UiKit.RoundedButton(bar, "ActionButton", TurkishTexts.ActionPerformAppraisal, 56, UiTheme.Gold, UiTheme.Ink, OnActionClicked);
            UiKit.AddShadow(action.gameObject, 0.18f, 8f);
            UiBuilder.Stretch(action.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(Gutter, 48f), new Vector2(-Gutter, 188f));
            _actionLabel = action.GetComponentInChildren<Text>();
            _actionLabel.fontStyle = FontStyle.Bold;
        }

        public void Show()
        {
            AppraisalScreenViewModel screen = _flow.AppraisalScreen;
            bool visible = _flow.CurrentScreen == UiScreen.Appraisal && screen != null;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _kicker.text = TurkishTexts.AppraisalTitle;
            _model.text = screen.Title;
            _subtitle.text = screen.Subtitle;
            _status.text = _flow.StatusMessage ?? string.Empty;
            _actionLabel.text = screen.ActionText;

            UiKit.Clear(_content);

            AddLevelsCard(screen);
            AddHeroCard(screen);
            if (screen.Result != null)
            {
                AddResult(screen.Result);
            }
            else
            {
                AddNote(screen.SelectedLevelId == null ? TurkishTexts.NoLevelSelected : TurkishTexts.LevelNotDone);
            }
        }

        // ---------- yardımcılar ----------

        private static Text Label(Transform parent, string name, string text, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Text label = UiBuilder.CreateWrappedText(parent, name, size, anchor, color);
            label.text = text ?? string.Empty;
            if (bold)
            {
                label.fontStyle = FontStyle.Bold;
            }

            return label;
        }

        private static RectTransform AutoCard(Transform parent, string name, Color fill, float spacing = 12f, int padding = 32)
        {
            RectTransform card = UiKit.Card(parent, name, fill);
            UiKit.Column(card, spacing, padding);
            return card;
        }

        // ---------- bölümler ----------

        private void AddLevelsCard(AppraisalScreenViewModel screen)
        {
            RectTransform card = AutoCard(_content, "LevelsCard", UiTheme.Card, 18f, 28);
            Label(card, "Header", TurkishTexts.LevelsCardHeader, 36, UiTheme.Muted, true);

            RectTransform row = UiBuilder.CreatePanel(card, "Row", new Color(0f, 0f, 0f, 0f));
            row.GetComponent<Image>().raycastTarget = false;
            UiKit.Size2(row, 0f, 232f);
            HorizontalLayoutGroup layout = UiKit.Row(row, 14f, 0);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (AppraisalLevelRowViewModel level in screen.Levels)
            {
                AddLevelChip(row, level);
            }
        }

        private void AddLevelChip(Transform row, AppraisalLevelRowViewModel level)
        {
            string levelId = level.LevelId;
            bool locked = level.IsLocked && !level.IsDone;
            Color fill = level.IsSelected ? UiTheme.Gold : (level.IsDone ? UiTheme.GoodSoft : (locked ? UiTheme.Disabled : UiTheme.CardSoft));
            Color ink = locked && !level.IsSelected ? UiTheme.Muted : UiTheme.Ink;

            Button chip = UiKit.RoundedButton(row, "Level_" + levelId, string.Empty, 30, fill, ink, () => _flow.SelectLevel(levelId));
            Text label = chip.GetComponentInChildren<Text>();
            label.gameObject.SetActive(false);
            RectTransform rect = chip.GetComponent<RectTransform>();

            Text code = Label(rect, "Code", level.Code, 52, ink, true, TextAnchor.MiddleCenter);
            UiBuilder.Stretch(code.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -74f), new Vector2(-8f, -12f));

            Text name = Label(rect, "Name", level.Name, 25, ink, false, TextAnchor.MiddleCenter);
            UiBuilder.Stretch(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 62f), new Vector2(-8f, -76f));

            Text status = Label(rect, "Status", level.FeeText + "\n" + level.StatusText, 22, level.IsSelected ? UiTheme.Ink : UiTheme.Muted, false, TextAnchor.MiddleCenter);
            UiBuilder.Stretch(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 6f), new Vector2(-6f, 64f));
        }

        private void AddHeroCard(AppraisalScreenViewModel screen)
        {
            RectTransform hero = UiKit.Card(_content, "HeroCard", UiTheme.Card);
            UiKit.Size2(hero, 0f, HeroHeight);

            if (screen.LevelBadge != null)
            {
                Text badge = UiKit.Badge(hero, "LevelBadge", UiTheme.GoodSoft, UiTheme.Good, 34);
                badge.text = screen.LevelBadge;
                badge.fontStyle = FontStyle.Bold;
                RectTransform badgeRect = badge.transform.parent as RectTransform;
                UiBuilder.Stretch(badgeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-470f, -92f), new Vector2(-24f, -24f));
            }

            RectTransform stage = UiKit.Rounded(hero, "Stage", UiTheme.CardSoft);
            UiBuilder.Stretch(stage, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 132f), new Vector2(-24f, -100f));
            stage.GetComponent<Image>().raycastTarget = false;

            var phone = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            phone.SetParent(stage, false);
            phone.anchorMin = new Vector2(0.5f, 0.5f);
            phone.anchorMax = new Vector2(0.5f, 0.5f);
            phone.sizeDelta = new Vector2(PhoneHeight, PhoneHeight);
            phone.localScale = new Vector3(0.9f, 0.9f, 1f);
            PhoneMockView.Draw(phone, screen.DefinitionId, _angle);

            RectTransform angles = UiBuilder.CreatePanel(hero, "Angles", new Color(0f, 0f, 0f, 0f));
            angles.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(angles, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-24f, 112f));
            HorizontalLayoutGroup layout = UiKit.Row(angles, 12f, 0);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            foreach (PhoneAngle angle in Angles)
            {
                PhoneAngle chosen = angle;
                bool selected = angle == _angle;
                UiKit.RoundedButton(angles, "Angle_" + angle, TurkishTexts.PhoneAngleName(angle), 32,
                    selected ? UiTheme.Ink : UiTheme.CardSoft, selected ? Color.white : UiTheme.Ink, () => OnAngleClicked(chosen));
            }
        }

        private void AddResult(AppraisalResultViewModel result)
        {
            AddMarketValueCard(result);
            AddInfoGrid(result);
            AddFindingsCard(result);
            AddRiskCard(result);
        }

        private void AddMarketValueCard(AppraisalResultViewModel result)
        {
            RectTransform card = AutoCard(_content, "MarketValueCard", UiTheme.Card, 8f, 34);
            Label(card, "Header", TurkishTexts.MarketValueHeader.ToUpperInvariant(), 32, UiTheme.Muted, true);
            Text value = Label(card, "Value", result.ValueHeadline ?? TurkishTexts.ValueRange(null), result.ValueHeadline == null ? 40 : 72, UiTheme.Ink, true);
            value.horizontalOverflow = HorizontalWrapMode.Wrap;

            RectTransform accent = UiKit.Rounded(card, "Accent", UiTheme.Gold);
            UiKit.Size2(accent, 0f, 8f);
            accent.GetComponent<Image>().raycastTarget = false;

            string footer = result.LevelName + " • " + result.FeeLine;
            Label(card, "Footer", footer, 32, UiTheme.Muted);
            if (!string.IsNullOrEmpty(result.SummaryLine))
            {
                Label(card, "Summary", result.SummaryLine, 32, UiTheme.Muted);
            }
        }

        private void AddInfoGrid(AppraisalResultViewModel result)
        {
            if (result.Cards.Count == 0)
            {
                return;
            }

            const float cellHeight = 200f;
            const float gap = 20f;

            // İki sütun: her satır yatay yerleşimdir (esnek genişlik); farklı ekran oranlarında kartlar orantılı kalır.
            for (int i = 0; i < result.Cards.Count; i += 2)
            {
                RectTransform row = UiBuilder.CreatePanel(_content, "InfoRow", new Color(0f, 0f, 0f, 0f));
                row.GetComponent<Image>().raycastTarget = false;
                UiKit.Size2(row, 0f, cellHeight);
                HorizontalLayoutGroup layout = UiKit.Row(row, gap, 0);
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;

                AddInfoCard(row, result.Cards[i]);
                if (i + 1 < result.Cards.Count)
                {
                    AddInfoCard(row, result.Cards[i + 1]);
                }
                else
                {
                    RectTransform filler = UiBuilder.CreatePanel(row, "Filler", new Color(0f, 0f, 0f, 0f));
                    filler.GetComponent<Image>().raycastTarget = false;
                }
            }
        }

        private static void AddInfoCard(Transform row, AppraisalInfoCardViewModel info)
        {
            RectTransform card = UiKit.Card(row, "Card_" + info.Title, UiTheme.Card);

            RectTransform dot = UiKit.Rounded(card, "Tone", UiTheme.ToneColor(info.Tone));
            dot.GetComponent<Image>().raycastTarget = false;
            UiBuilder.Stretch(dot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -62f), new Vector2(52f, -38f));

            Text title = Label(card, "Title", info.Title, 34, UiTheme.Muted, true);
            UiBuilder.Stretch(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(68f, -72f), new Vector2(-20f, -28f));

            Text value = Label(card, "Value", info.Value, 40, UiTheme.Ink, true);
            UiBuilder.Stretch(value.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 54f), new Vector2(-20f, -84f));

            if (!string.IsNullOrEmpty(info.Note))
            {
                Text note = Label(card, "Note", info.Note, 28, UiTheme.Muted);
                UiBuilder.Stretch(note.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 16f), new Vector2(-20f, 56f));
            }
        }

        private void AddFindingsCard(AppraisalResultViewModel result)
        {
            if (result.Findings.Count == 0)
            {
                return;
            }

            RectTransform card = AutoCard(_content, "FindingsCard", UiTheme.Card, 14f, 32);
            Label(card, "Header", TurkishTexts.FindingsHeader, 40, UiTheme.Ink, true);

            foreach (AppraisalFindingViewModel finding in result.Findings)
            {
                RectTransform row = UiBuilder.CreatePanel(card, "Finding", new Color(0f, 0f, 0f, 0f));
                row.GetComponent<Image>().raycastTarget = false;
                HorizontalLayoutGroup layout = UiKit.Row(row, 18f, 10, TextAnchor.UpperLeft);
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                RectTransform dot = UiKit.Rounded(row, "Tone", UiTheme.ToneColor(finding.Tone));
                dot.GetComponent<Image>().raycastTarget = false;
                UiKit.Size2(dot, 34f, 34f);

                Text text = Label(row, "Text", finding.Text + "\n<size=28><color=#6B7890>" + finding.ConfidenceText + "</color></size>", 38, UiTheme.Ink);
                UiKit.Flex(text.rectTransform, 1f);
            }
        }

        private void AddRiskCard(AppraisalResultViewModel result)
        {
            RectTransform card = AutoCard(_content, "RiskCard", UiTheme.WarnSoft, 10f, 32);
            Label(card, "Header", TurkishTexts.RiskHeader, 40, UiTheme.Ink, true);
            if (result.RiskNote != null)
            {
                Label(card, "Note", result.RiskNote, 36, UiTheme.Ink);
                return;
            }

            Label(card, "Title", result.RiskTitle, 34, UiTheme.Muted);
            foreach (string line in result.RiskLines)
            {
                Label(card, "Scenario", line, 34, UiTheme.Ink);
            }

            Label(card, "Miss", result.MissLine, 34, UiTheme.Ink, true);
        }

        private void AddNote(string text)
        {
            RectTransform card = AutoCard(_content, "NoteCard", UiTheme.Card, 8f, 36);
            Label(card, "Note", text, 40, UiTheme.Ink, false, TextAnchor.MiddleCenter);
        }

        // ---------- tıklamalar ----------

        private void OnAngleClicked(PhoneAngle angle)
        {
            _angle = angle;
            Show();
        }

        private void OnBackClicked()
        {
            _flow.Back();
        }

        private void OnActionClicked()
        {
            _flow.PerformAppraisal();
        }
    }
}
