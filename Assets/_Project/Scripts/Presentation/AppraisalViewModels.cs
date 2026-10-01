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

        /// <summary>Kısa seviye kodu (S0, S1, S2, S3).</summary>
        public string Code { get; }

        public AppraisalLevelRowViewModel(string levelId, string name, string feeText, string statusText, bool isDone, bool isLocked, bool isSelected, string code = null)
        {
            Code = code ?? levelId;
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

        /// <summary>Bulgular (ton + metin + güven); <see cref="FindingLines"/> ile aynı gerçek bulgulardan.</summary>
        public IReadOnlyList<AppraisalFindingViewModel> Findings { get; }

        /// <summary>Bilgi kartları (Ekran, Kamera, Pil, Kasa); yalnızca sonuçta veri olanlar.</summary>
        public IReadOnlyList<AppraisalInfoCardViewModel> Cards { get; }

        /// <summary>"29.000 ₺ – 33.000 ₺"; bu seviyede değer verilmiyorsa null.</summary>
        public string ValueHeadline { get; }

        /// <summary>Bulgu sayıları (ör. "2 uyarı • 1 temiz"); bulgu yoksa boş.</summary>
        public string SummaryLine { get; }

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
            string riskNote,
            IEnumerable<AppraisalFindingViewModel> findings = null,
            IEnumerable<AppraisalInfoCardViewModel> cards = null,
            string valueHeadline = null,
            string summaryLine = null)
        {
            Findings = ReadOnly.List(findings);
            Cards = ReadOnly.List(cards);
            ValueHeadline = valueHeadline;
            SummaryLine = summaryLine ?? string.Empty;
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

        /// <summary>"256 GB • 31 Aylık".</summary>
        public string Subtitle { get; }

        /// <summary>Seçili seviyenin rozeti ("S2 • Ayrıntılı kontrol"); seçim yoksa null.</summary>
        public string LevelBadge { get; }

        /// <summary>Telefon modelinin içerik kimliği (ileride gerçek ürün görselini bağlamak için).</summary>
        public string DefinitionId { get; }

        public AppraisalScreenViewModel(
            string title,
            IEnumerable<AppraisalLevelRowViewModel> levels,
            string selectedLevelId,
            AppraisalResultViewModel result,
            string actionText,
            string subtitle = null,
            string levelBadge = null,
            string definitionId = null)
        {
            DefinitionId = definitionId;
            Subtitle = subtitle ?? string.Empty;
            LevelBadge = levelBadge;
            Title = title;
            Levels = new ReadOnlyCollection<AppraisalLevelRowViewModel>(new List<AppraisalLevelRowViewModel>(levels));
            SelectedLevelId = selectedLevelId;
            Result = result;
            ActionText = actionText;
        }
    }
}
