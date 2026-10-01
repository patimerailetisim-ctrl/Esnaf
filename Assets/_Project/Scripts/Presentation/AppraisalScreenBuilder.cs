using System.Collections.Generic;
using Esnaf.Core;
using Esnaf.Domain.Appraisal;
using Esnaf.Domain.Game;
using Esnaf.Domain.Market;

namespace Esnaf.Presentation
{
    /// <summary>
    /// Ekspertiz ekranının görünüm modelini IGameApi sonuçlarından kurar. Kural yoktur: ücret, kilit ve sonuç oyundan gelir;
    /// kilit/ücret yazıları yalnızca içerikteki verinin gösterimidir (gerçek karar StartAppraisal'dadır).
    /// </summary>
    internal static class AppraisalScreenBuilder
    {
        public static AppraisalScreenViewModel Build(
            ListingView listing,
            IReadOnlyList<AppraisalView> known,
            int day,
            string selectedLevelId,
            ContentPresentation content,
            IGameApi api)
        {
            var rows = new List<AppraisalLevelRowViewModel>();
            foreach (AppraisalLevelInfo level in content.AppraisalLevels)
            {
                bool done = Find(known, level.Id) != null;
                bool locked = day < level.UnlockDay;
                Money fee;
                string feeText = content.TryGetFee(level.Id, listing.DefinitionId, out fee) ? TurkishTexts.LevelFee(fee) : string.Empty;
                rows.Add(new AppraisalLevelRowViewModel(
                    level.Id,
                    level.Name,
                    feeText,
                    StatusOf(done, locked, level),
                    done,
                    locked,
                    level.Id == selectedLevelId));
            }

            AppraisalView selected = selectedLevelId == null ? null : Find(known, selectedLevelId);
            AppraisalResultViewModel result = selected == null ? null : BuildResult(selected, listing, content, api);
            return new AppraisalScreenViewModel(
                ListingDetailViewModel.From(listing, content).Title,
                rows,
                selectedLevelId,
                result,
                selected == null ? TurkishTexts.ActionPerformAppraisal : TurkishTexts.ActionShowAppraisal);
        }

        private static string StatusOf(bool done, bool locked, AppraisalLevelInfo level)
        {
            if (done)
            {
                return TurkishTexts.LevelDone;
            }

            if (locked)
            {
                return TurkishTexts.LevelLocked(level.UnlockDay);
            }

            return level.RequiresEquipment ? TurkishTexts.LevelNeedsEquipment : TurkishTexts.LevelReady;
        }

        private static AppraisalView Find(IReadOnlyList<AppraisalView> known, string levelId)
        {
            foreach (AppraisalView view in known)
            {
                if (view.LevelId == levelId)
                {
                    return view; // aynı ilan + aynı seviye için tek sonuç vardır (I5)
                }
            }

            return null;
        }

        private static AppraisalResultViewModel BuildResult(AppraisalView view, ListingView listing, ContentPresentation content, IGameApi api)
        {
            var findings = new List<string>();
            foreach (FindingView finding in view.Findings)
            {
                findings.Add(TurkishTexts.Finding(finding.Found, finding.WordingKey, finding.Attribute, finding.Confidence));
            }

            string levelName = view.LevelId;
            foreach (AppraisalLevelInfo level in content.AppraisalLevels)
            {
                if (level.Id == view.LevelId)
                {
                    levelName = level.Name;
                }
            }

            string riskTitle = null;
            string missLine = null;
            string riskNote = null;
            var riskLines = new List<string>();
            Result<RiskCard> card = api.GetRiskCard(view.ResultId, listing.AskingPrice);
            if (card.IsSuccess)
            {
                riskTitle = TurkishTexts.RiskTitle(listing.AskingPrice);
                foreach (RiskScenario scenario in card.Value.Scenarios)
                {
                    riskLines.Add(TurkishTexts.RiskScenarioLine(scenario));
                }

                missLine = TurkishTexts.MissProbability(card.Value.MissProbability);
            }
            else
            {
                riskNote = TurkishTexts.Error(card.ErrorCode);
            }

            return new AppraisalResultViewModel(
                levelName,
                TurkishTexts.PaidFee(view.Fee),
                findings,
                TurkishTexts.Battery(view.BatteryRange),
                TurkishTexts.Body(view.BodyRange),
                TurkishTexts.ValueRange(view.ValueRange),
                riskTitle,
                riskLines,
                missLine,
                riskNote);
        }
    }
}
