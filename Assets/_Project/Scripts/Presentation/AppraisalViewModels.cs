using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Presentation
{
    /// <summary>Ekspertiz seviyesinin satırı: ad, ücret, durum (yapıldı / kilitli / cihaz gerekir / hazır). IsLocked yalnızca gösterimdir; kararı API verir.</summary>
    public sealed class AppraisalLevelRowViewModel
    {
        public string LevelId { get; }
        public string Name { get; }
        public string FeeText { get; }
        public string StatusText { get; }
        public bool IsDone { get; }
        public bool IsLocked { get; }
        public bool IsSelected { get; }

        public AppraisalLevelRowViewModel(string levelId, string name, string feeText, string statusText, bool isDone, bool isLocked, bool isSelected)
        {
            LevelId = levelId;
            Name = name;
            FeeText = feeText;
            StatusText = statusText;
            IsDone = isDone;
            IsLocked = isLocked;
            IsSelected = isSelected;
        }
    }

    /// <summary>
    /// Bir ekspertiz sonucunun ekranı (hepsi IGameApi sonuçlarından hazır Türkçe satırlar). Risk kartı yoksa <see cref="RiskNote"/> nedenini söyler.
    /// </summary>
    public sealed class AppraisalResultViewModel
    {
        public string LevelName { get; }
        public string FeeLine { get; }
        public IReadOnlyList<string> FindingLines { get; }
        public string BatteryLine { get; }
        public string BodyLine { get; }
        public string ValueLine { get; }
        public string RiskTitle { get; }
        public IReadOnlyList<string> RiskLines { get; }
        public string MissLine { get; }
        public string RiskNote { get; }

        public AppraisalResultViewModel(
            string levelName,
            string feeLine,
            IEnumerable<string> findingLines,
            string batteryLine,
            string bodyLine,
            string valueLine,
            string riskTitle,
            IEnumerable<string> riskLines,
            string missLine,
            string riskNote)
        {
            LevelName = levelName;
            FeeLine = feeLine;
            FindingLines = new ReadOnlyCollection<string>(new List<string>(findingLines));
            BatteryLine = batteryLine;
            BodyLine = bodyLine;
            ValueLine = valueLine;
            RiskTitle = riskTitle;
            RiskLines = new ReadOnlyCollection<string>(new List<string>(riskLines));
            MissLine = missLine;
            RiskNote = riskNote;
        }
    }

    /// <summary>Ekspertiz ekranı: seviyeler, seçili seviye, (varsa) seçili seviyenin sonucu ve ana düğmenin yazısı.</summary>
    public sealed class AppraisalScreenViewModel
    {
        public string Title { get; }
        public IReadOnlyList<AppraisalLevelRowViewModel> Levels { get; }
        public string SelectedLevelId { get; }
        public AppraisalResultViewModel Result { get; }
        public string ActionText { get; }

        public AppraisalScreenViewModel(string title, IEnumerable<AppraisalLevelRowViewModel> levels, string selectedLevelId, AppraisalResultViewModel result, string actionText)
        {
            Title = title;
            Levels = new ReadOnlyCollection<AppraisalLevelRowViewModel>(new List<AppraisalLevelRowViewModel>(levels));
            SelectedLevelId = selectedLevelId;
            Result = result;
            ActionText = actionText;
        }
    }
}
