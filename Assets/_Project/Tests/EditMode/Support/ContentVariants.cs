using System.Collections.Generic;
using System.IO;
using Esnaf.Domain.Content;
using NUnit.Framework;

namespace Esnaf.Tests.Support
{
    /// <summary>Gerçek içerik dosyalarının, tek bir metni değiştirilmiş kopyası (ör. düşük sermaye). Gerçek JSON'a dokunmaz.</summary>
    public static class ContentVariants
    {
        private sealed class DictionarySource : IContentSource
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public DictionarySource(string directory, string from, string to)
            {
                foreach (string path in Directory.GetFiles(directory, "*.json"))
                {
                    _files[Path.GetFileName(path)] = File.ReadAllText(path).Replace(from, to);
                }
            }

            public bool TryGetText(string fileName, out string text)
            {
                return _files.TryGetValue(fileName, out text);
            }
        }

        public static ContentDatabase WithOpeningCapital(long tl)
        {
            ContentLoadResult loaded = ContentDatabase.Load(new DictionarySource(TestPaths.ContentDataDirectory(), "\"openingCapital\": 250000", "\"openingCapital\": " + tl));
            Assert.IsTrue(loaded.IsSuccess, loaded.FormatIssues());
            return loaded.Database;
        }
    }
}
