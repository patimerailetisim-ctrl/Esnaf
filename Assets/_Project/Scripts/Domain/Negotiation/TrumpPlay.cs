using System;
using Esnaf.Core;

namespace Esnaf.Domain.Negotiation
{
    /// <summary>
    /// Pazarlıkta oynanan bir koz kartı. Motor kartın doğru mu yanlış alarm mı olduğunu bilir (oyuncu bilmez).
    /// Anahtar (<see cref="Key"/>) bir pazarlıkta kartın yalnızca bir kez oynanmasını sağlar.
    /// </summary>
    public sealed class TrumpPlay
    {
        public string Key { get; }
        public bool IsValid { get; }
        public Money ProblemValue { get; }
        public double EvidencePower { get; }

        /// <summary>Kart profesyonel (S3) rapordan geliyorsa satıcıyı ek olarak ikna eder.</summary>
        public bool IsProfessionalReport { get; }

        public TrumpPlay(string key, bool isValid, Money problemValue, double evidencePower, bool isProfessionalReport)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Card key is required.", nameof(key));
            }

            Key = key;
            IsValid = isValid;
            ProblemValue = problemValue;
            EvidencePower = evidencePower;
            IsProfessionalReport = isProfessionalReport;
        }
    }
}
