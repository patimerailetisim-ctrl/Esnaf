using System.Text;
using Esnaf.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Esnaf.App.Ui
{
    /// <summary>
    /// Ekspertiz ekranı: seviye listesi (ücret ve durum), seçili seviyenin sonucu (bulgular, pil/gövde aralığı, değer aralığı, risk kartı),
    /// durum mesajı, "Geri" ve ana düğme. Yalnızca <see cref="UiFlow"/> ile konuşur (AppraisalScreen, SelectLevel, PerformAppraisal, Back);
    /// ekspertiz kuralı yoktur: ücret, kilit ve sonuç IGameApi'den gelir. Liste her değişimde yeniden kurulur.
    /// </summary>
    internal sealed class AppraisalPanelView
    {
        private const float BackHeight = 120f;
        private const float StatusHeight = 90f;
        private const float ActionHeight = 170f;

        private static readonly Color RowColor = new Color(0.18f, 0.2f, 0.26f, 1f);
        private static readonly Color SelectedRowColor = new Color(0.2f, 0.4f, 0.65f, 1f);
        private static readonly Color LockedRowColor = new Color(0.14f, 0.15f, 0.18f, 1f);

        private readonly UiFlow _flow;
        private readonly GameObject _root;
        private readonly Text _title;
        private readonly RectTransform _content;
        private readonly Text _status;
        private readonly Text _actionLabel;

        public AppraisalPanelView(Transform canvas, UiFlow flow)
        {
            _flow = flow;

            RectTransform root = UiBuilder.CreatePanel(canvas, "AppraisalScreen", new Color(0.09f, 0.1f, 0.13f, 1f));
            _root = root.gameObject;
            UiBuilder.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TopBarView.Height));

            Button back = UiBuilder.CreateButton(root, "BackButton", TurkishTexts.BackButton, 44, new Color(0.3f, 0.33f, 0.42f, 1f), OnBackClicked);
            UiBuilder.Stretch(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.4f, 1f), new Vector2(40f, -BackHeight - 20f), new Vector2(0f, -20f));

            _title = UiBuilder.CreateText(root, "Title", 52, TextAnchor.MiddleLeft, Color.white);
            UiBuilder.Stretch(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -BackHeight - 150f), new Vector2(-40f, -BackHeight - 30f));

            _content = UiBuilder.CreateVerticalList(root, "List", new Color(0.09f, 0.1f, 0.13f, 1f), 14f, 24f);
            RectTransform list = _content.parent as RectTransform;
            UiBuilder.Stretch(list, Vector2.zero, Vector2.one, new Vector2(0f, ActionHeight + StatusHeight), new Vector2(0f, -BackHeight - 160f));

            _status = UiBuilder.CreateText(root, "Status", 38, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.45f, 1f));
            UiBuilder.Stretch(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, ActionHeight), new Vector2(-40f, ActionHeight + StatusHeight));

            Button action = UiBuilder.CreateButton(root, "ActionButton", TurkishTexts.ActionPerformAppraisal, 52, new Color(0.25f, 0.5f, 0.45f, 1f), OnActionClicked);
            UiBuilder.Stretch(action.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 25f), new Vector2(-40f, ActionHeight - 25f));
            _actionLabel = action.GetComponentInChildren<Text>();
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

            _title.text = TurkishTexts.AppraisalTitle + " — " + screen.Title;
            _status.text = _flow.StatusMessage ?? string.Empty;
            _actionLabel.text = screen.ActionText;

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_content.GetChild(i).gameObject);
            }

            AddHeader(TurkishTexts.LevelsHeader);
            foreach (AppraisalLevelRowViewModel level in screen.Levels)
            {
                AddLevelRow(level);
            }

            if (screen.Result != null)
            {
                AddHeader(TurkishTexts.ResultHeader);
                AddResult(screen.Result);
            }
            else if (screen.SelectedLevelId != null)
            {
                AddNote(TurkishTexts.LevelNotDone);
            }
        }

        private void AddHeader(string text)
        {
            Text header = UiBuilder.CreateWrappedText(_content, "Header", 44, TextAnchor.MiddleLeft, new Color(0.7f, 0.85f, 1f, 1f));
            header.text = text;
            header.gameObject.AddComponent<LayoutElement>().minHeight = 70f;
        }

        private void AddNote(string text)
        {
            Text note = UiBuilder.CreateWrappedText(_content, "Note", 40, TextAnchor.MiddleLeft, new Color(0.85f, 0.87f, 0.92f, 1f));
            note.text = text;
            note.gameObject.AddComponent<LayoutElement>().minHeight = 70f;
        }

        private void AddLevelRow(AppraisalLevelRowViewModel level)
        {
            string levelId = level.LevelId;
            Color color = level.IsSelected ? SelectedRowColor : (level.IsLocked && !level.IsDone ? LockedRowColor : RowColor);
            Button button = UiBuilder.CreateButton(_content, "Level_" + levelId, string.Empty, 40, color, () => _flow.SelectLevel(levelId));
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 150f;
            layout.preferredHeight = 150f;

            Text label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 38;
            label.text = level.Name + "\n" + level.FeeText + "  •  " + level.StatusText;
            UiBuilder.Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f));
        }

        private void AddResult(AppraisalResultViewModel result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(result.LevelName);
            sb.AppendLine(result.FeeLine);
            sb.AppendLine();
            foreach (string line in result.FindingLines)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine(result.BatteryLine);
            sb.AppendLine(result.BodyLine);
            sb.AppendLine(result.ValueLine);
            sb.AppendLine();
            if (result.RiskNote != null)
            {
                sb.Append(result.RiskNote);
            }
            else
            {
                sb.AppendLine(result.RiskTitle);
                foreach (string line in result.RiskLines)
                {
                    sb.AppendLine(line);
                }

                sb.Append(result.MissLine);
            }

            Text body = UiBuilder.CreateWrappedText(_content, "Result", 38, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.95f, 1f));
            body.text = sb.ToString();
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
