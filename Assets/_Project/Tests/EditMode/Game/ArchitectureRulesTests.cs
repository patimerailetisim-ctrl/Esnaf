using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Game
{
    /// <summary>
    /// GDD değişmez kuralları kaynak kodda taranır: Unity'den bağımsız kural katmanı (T4), gerçek saat yok (K8),
    /// System.Random/UnityEngine.Random yok (K9), singleton yok (K7), C# 9 uyumu (T19: record/init yok).
    /// Yalnızca yorum dışı kod satırları taranır.
    /// </summary>
    public class ArchitectureRulesTests
    {
        private static readonly string[] RuleLayers = { "Core", "Domain", "Persistence" };

        private static string ScriptsDirectory()
        {
            // .../Assets/_Project/Content/Data -> .../Assets/_Project/Scripts
            DirectoryInfo data = new DirectoryInfo(TestPaths.ContentDataDirectory());
            return Path.Combine(data.Parent.Parent.FullName, "Scripts");
        }

        private static IEnumerable<KeyValuePair<string, string[]>> RuleSources()
        {
            string scripts = ScriptsDirectory();
            foreach (string layer in RuleLayers)
            {
                string dir = Path.Combine(scripts, layer);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string[] code = File.ReadAllLines(file).Select(StripComment).ToArray();
                    yield return new KeyValuePair<string, string[]>(file.Substring(scripts.Length + 1), code);
                }
            }
        }

        private static string StripComment(string line)
        {
            int at = line.IndexOf("//", StringComparison.Ordinal);
            return at >= 0 ? line.Substring(0, at) : line;
        }

        private static void AssertNoMatch(string pattern, string explanation)
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            var hits = new List<string>();
            int scanned = 0;
            foreach (KeyValuePair<string, string[]> source in RuleSources())
            {
                scanned++;
                for (int i = 0; i < source.Value.Length; i++)
                {
                    if (regex.IsMatch(source.Value[i]))
                    {
                        hits.Add(source.Key + ":" + (i + 1) + ": " + source.Value[i].Trim());
                    }
                }
            }

            Assert.Greater(scanned, 50, "Kaynak dosyaları bulunamadı; tarama anlamsız");
            Assert.AreEqual(0, hits.Count, explanation + "\n" + string.Join("\n", hits));
        }

        [Test]
        public void RuleLayers_DoNotReferenceUnity()
        {
            AssertNoMatch(@"\busing\s+UnityEngine|\bUnityEngine\.|\busing\s+UnityEditor|\bUnityEditor\.", "T4: kural katmanı Unity'ye bağlanamaz.");
        }

        [Test]
        public void RuleLayers_DoNotUseTheSystemClock()
        {
            AssertNoMatch(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b|\bEnvironment\.TickCount|\bStopwatch\b|\bTime\.time\b", "K8: kural kodunda gerçek saat yasak.");
        }

        [Test]
        public void RuleLayers_DoNotUseUnseededRandomness()
        {
            AssertNoMatch(@"\bSystem\.Random\b|\bnew\s+Random\s*\(|\bRandom\.(Range|value|Next)|\bGuid\.NewGuid", "K9: yalnızca enjekte edilen IRandom akışları kullanılır.");
        }

        [Test]
        public void RuleLayers_HaveNoSingletons()
        {
            AssertNoMatch(@"\bstatic\b[^;=(){}]*\bInstance\b\s*(\{|;|=|=>)", "K7: global Instance yok; bağımlılıklar constructor'la verilir.");
        }

        [Test]
        public void RuleLayers_UseCSharp9Idioms_NoRecordsOrInitOnly()
        {
            AssertNoMatch(@"\brecord\s+(class|struct)\b|\b(public|internal)\s+(sealed\s+)?record\s+\w|\{\s*get;\s*init;\s*\}|\binit\s*;", "T19: record/init yok (Unity uyumu).");
        }

        [Test]
        public void RuleLayers_HaveNoMutablePublicStatics()
        {
            AssertNoMatch(@"\bpublic\s+static\s+(?!readonly\b)(?!class\b)(?!void\b)(?!bool\b)(?!string\b)(?!int\b)(?!long\b)(?!double\b)(?!Money\b)(?!Result)(?!IReadOnly)\w[\w<>,\[\]\s\.]*\s+\w+\s*(=|;)", "Değiştirilebilir genel statik durum yok.");
        }
    }
}
