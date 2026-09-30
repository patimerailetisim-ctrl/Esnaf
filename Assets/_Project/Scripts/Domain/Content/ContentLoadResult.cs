using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Esnaf.Domain.Content
{
    /// <summary>İçerik yükleme sonucu: başarılıysa <see cref="Database"/> dolu; hata varsa null. Uyarılar başarıda da bulunabilir.</summary>
    public sealed class ContentLoadResult
    {
        public ContentDatabase Database { get; }
        public IReadOnlyList<ContentIssue> Issues { get; }

        public bool IsSuccess
        {
            get { return Database != null; }
        }

        public int ErrorCount
        {
            get { return Count(ContentIssueSeverity.Error); }
        }

        public int WarningCount
        {
            get { return Count(ContentIssueSeverity.Warning); }
        }

        internal ContentLoadResult(ContentDatabase database, IList<ContentIssue> issues)
        {
            Database = database;
            Issues = new ReadOnlyCollection<ContentIssue>(new List<ContentIssue>(issues));
        }

        /// <summary>Tüm sorunları satır satır metne döker (log/menü için).</summary>
        public string FormatIssues()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < Issues.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(Issues[i]);
            }

            return builder.ToString();
        }

        private int Count(ContentIssueSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
