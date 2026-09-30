using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esnaf.Domain.Economy
{
    /// <summary>
    /// MVP'nin 8 çekirdek defter türü (GDD v0.2 9.3). Ekonomi kodu bunlara dayanır; bu yüzden doğrulayıcı hepsinin varlığını
    /// ve beklenen davranışını denetler. Başka türler serbestçe eklenebilir.
    /// </summary>
    public static class TransactionTypeIds
    {
        public const string OpeningCapital = "opening_capital";
        public const string Purchase = "purchase";
        public const string Sale = "sale";
        public const string Appraisal = "appraisal";
        public const string WastedAppraisal = "wasted_appraisal";
        public const string Repair = "repair";
        public const string DailyExpense = "daily_expense";
        public const string Investment = "investment";

        public static readonly IReadOnlyList<string> Required = new ReadOnlyCollection<string>(new[]
        {
            OpeningCapital, Purchase, Sale, Appraisal, WastedAppraisal, Repair, DailyExpense, Investment
        });
    }
}
