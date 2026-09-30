#if UNITY_EDITOR
using System.IO;
using System.Text;
using Esnaf.Domain.Content;
using UnityEditor;
using UnityEngine;

namespace Esnaf.Content
{
    /// <summary>
    /// Esnaf > Validate Content menüsü: Assets/_Project/Content/Data altındaki JSON'ları Domain'in ContentDatabase.Load'ı ile
    /// doğrular (oyun ve simülatörle AYNI kod). Sonucu Console'a yazar.
    /// </summary>
    internal static class ContentValidationMenu
    {
        private const string DataFolder = "_Project/Content/Data";

        [MenuItem("Esnaf/Validate Content")]
        private static void ValidateContent()
        {
            string directory = Path.Combine(Application.dataPath, DataFolder);
            ContentLoadResult result = ContentDatabase.Load(new DirectoryContentSource(directory));

            foreach (ContentIssue issue in result.Issues)
            {
                if (issue.Severity == ContentIssueSeverity.Error)
                {
                    Debug.LogError(issue.ToString());
                }
                else
                {
                    Debug.LogWarning(issue.ToString());
                }
            }

            var summary = new StringBuilder();
            if (result.IsSuccess)
            {
                summary.Append("Content OK: ").Append(result.Database.Products.Count).Append(" product models loaded");
                summary.Append(" (").Append(result.WarningCount).Append(" warnings).");
                Debug.Log(summary.ToString());
            }
            else
            {
                summary.Append("Content INVALID: ").Append(result.ErrorCount).Append(" errors, ");
                summary.Append(result.WarningCount).Append(" warnings. See the Console.");
                Debug.LogError(summary.ToString());
            }

            EditorUtility.DisplayDialog("Esnaf: Validate Content", summary.ToString(), "OK");
        }
    }
}
#endif
