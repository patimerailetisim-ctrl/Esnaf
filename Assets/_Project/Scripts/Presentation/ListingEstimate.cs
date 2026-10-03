using System.Collections.Generic;
using Esnaf.Domain.Appraisal;

namespace Esnaf.Presentation
{
    /// <summary>
    /// "Tahmini değer" satırı (Gün 13.3): yeni formül YOK ve gizli bilgi sızmaz. İlanın gerçek değeri ve referans fiyatı gizli kalır; tahmin yalnızca mevcut ekspertiz sisteminin
    /// (IGameApi.GetAppraisals) değer aralığından gelir (en son değer aralığı olan ekspertiz). Ekspertiz yoksa "ekspertizle öğrenilir". "Kârlı/kötü ilan" kararı verilmez.
    /// </summary>
    internal static class ListingEstimate
    {
        public static string Text(IReadOnlyList<AppraisalView> appraisals)
        {
            if (appraisals != null)
            {
                for (int i = appraisals.Count - 1; i >= 0; i--)
                {
                    MoneyRange range = appraisals[i].ValueRange;
                    if (range != null)
                    {
                        return TurkishTexts.EstimatedFromAppraisal(range.Min, range.Max);
                    }
                }
            }

            return TurkishTexts.EstimatedUnknown;
        }
    }
}
