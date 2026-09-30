using System;
using System.IO;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// Depodaki gerçek içerik klasörünü bulur. Hem `dotnet test` (bin/... altından çalışır) hem Unity Test Runner
    /// (çalışma dizini proje kökü) için: aday başlangıç dizinlerinden yukarı doğru "Assets/_Project/Content/Data" arar.
    /// </summary>
    public static class TestPaths
    {
        private static readonly string[] RelativeContentPath = { "Assets", "_Project", "Content", "Data" };

        public static string ContentDataDirectory()
        {
            string[] starts = { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() };
            foreach (string start in starts)
            {
                string found = Search(start);
                if (found != null)
                {
                    return found;
                }
            }

            throw new DirectoryNotFoundException(
                "Could not locate Assets/_Project/Content/Data from '" + starts[0] + "' or '" + starts[1] + "'.");
        }

        /// <summary>Assets/_Project/Tests/Fixtures (golden dosyaları).</summary>
        public static string FixturesDirectory()
        {
            return Path.Combine(Directory.GetParent(Directory.GetParent(ContentDataDirectory()).FullName).FullName, "Tests", "Fixtures");
        }

        private static string Search(string start)
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, Path.Combine(RelativeContentPath));
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
