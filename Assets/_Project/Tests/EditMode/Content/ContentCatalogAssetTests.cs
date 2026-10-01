using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Esnaf.Domain.Content;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    /// <summary>
    /// Unity oyunu içeriği ContentCatalog.asset'in listelediği TextAsset'lerden okur (Assets/_Project/Content/Data klasöründen DEĞİL).
    /// Yeni bir JSON eklenip katalog güncellenmezse oyun o dosyayı hiç görmez: "manifest.id_missing_in_content" gibi hatalar çıkar (Gün 11.2.3 regresyonu).
    /// Bu testler katalog dosyasını (YAML) GUID'lerle klasördeki gerçek dosyalara karşı doğrular. Unity'siz çalışır.
    /// </summary>
    public class ContentCatalogAssetTests
    {
        private static string ContentDir()
        {
            return Directory.GetParent(TestPaths.ContentDataDirectory()).FullName;
        }

        private static Dictionary<string, string> JsonByGuid()
        {
            var map = new Dictionary<string, string>();
            foreach (string meta in Directory.GetFiles(TestPaths.ContentDataDirectory(), "*.json.meta"))
            {
                string guid = Regex.Match(File.ReadAllText(meta), @"^guid: (\w+)", RegexOptions.Multiline).Groups[1].Value;
                map.Add(guid, meta.Substring(0, meta.Length - ".meta".Length));
            }

            return map;
        }

        private static List<string> CatalogGuids()
        {
            return Regex.Matches(File.ReadAllText(Path.Combine(ContentDir(), "ContentCatalog.asset")), @"^\s+- \{fileID: 4900000, guid: (\w+), type: 3\}", RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }

        [Test]
        public void TheCatalog_ListsEveryContentJsonFile_NoneMissingNoneExtra()
        {
            Dictionary<string, string> onDisk = JsonByGuid();
            List<string> listed = CatalogGuids();

            Assert.AreEqual(listed.Count, listed.Distinct().Count(), "aynı dosya iki kez listelenmiş");
            var missing = onDisk.Where(p => !listed.Contains(p.Key)).Select(p => Path.GetFileName(p.Value)).ToList();
            var extra = listed.Where(g => !onDisk.ContainsKey(g)).ToList();
            Assert.IsEmpty(missing, "Katalogda olmayan içerik dosyaları (Esnaf > Setup Day 10 çalıştırın): " + string.Join(", ", missing));
            Assert.IsEmpty(extra, "Klasörde karşılığı olmayan katalog girişleri");
        }

        [Test]
        public void TheGame_LoadsFromTheCatalogFilesAlone_WithoutManifestErrors()
        {
            // Unity'nin yaptığı: yalnızca katalogdaki dosyaları kaynak yapmak.
            Dictionary<string, string> onDisk = JsonByGuid();
            var source = new DictionaryContentSource();
            foreach (string guid in CatalogGuids())
            {
                string path = onDisk[guid];
                source.Add(Path.GetFileName(path), File.ReadAllText(path));
            }

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsTrue(result.IsSuccess, result.FormatIssues());
            Assert.IsFalse(result.Issues.Any(i => i.Code == ContentIssueCodes.ManifestIdMissingInContent));
            Assert.AreEqual(6, result.Database.Accessories.Definitions.Count);
            Assert.AreEqual(6, result.Database.Wholesale.Offers.Count);
        }

        [Test]
        public void ACatalogMissingTheAccessoryFiles_ReproducesTheManifestError()
        {
            // Regresyonun kendisi: aksesuar dosyaları kataloktan dışarıda kalırsa manifest "içerikte yok" der (manifest doğru, katalog eski).
            var source = new DictionaryContentSource();
            foreach (string path in Directory.GetFiles(TestPaths.ContentDataDirectory(), "*.json"))
            {
                string name = Path.GetFileName(path);
                if (name != ContentFileNames.Accessories && name != ContentFileNames.Wholesale)
                {
                    source.Add(name, File.ReadAllText(path));
                }
            }

            ContentLoadResult result = ContentDatabase.Load(source);

            Assert.IsFalse(result.IsSuccess);
            var missing = result.Issues.Where(i => i.Code == ContentIssueCodes.ManifestIdMissingInContent).Select(i => i.Message).ToList();
            Assert.IsTrue(missing.Any(m => m.Contains("accessory.charger_adapter")));
            Assert.IsTrue(missing.Any(m => m.Contains("supplier.ucuz_toptan")));
        }

        [Test]
        public void TheManifest_ListsExactlyTheIdsTheRealContentProduces()
        {
            ContentDatabase content = MarketHarness.RealContent();
            var produced = new List<string>();
            produced.AddRange(content.Products.Select(p => p.Id));
            produced.AddRange(content.Npcs.Select(n => n.Id));
            produced.AddRange(content.Accessories.Definitions.Select(a => a.Id));
            produced.AddRange(content.Wholesale.Offers.Select(o => o.SupplierId).Distinct());

            string manifest = File.ReadAllText(Path.Combine(TestPaths.ContentDataDirectory(), "content_id_manifest.json"));
            var listed = Regex.Matches(manifest, "\"((?:phone|npc|accessory|supplier)\\.[a-z0-9_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();

            CollectionAssert.AreEquivalent(produced, listed);
        }

        // ---------- Day10Setup: Play Mode'da sahne üretimi yok ----------

        [Test]
        public void TheSceneCreationCall_ExistsOnlyInTheEditorSetup_AndIsGuardedAgainstPlayMode()
        {
            string scripts = Path.Combine(Directory.GetParent(ContentDir()).FullName, "Scripts");
            var callers = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains("EditorSceneManager.NewScene"))
                .Select(Path.GetFileName).ToList();

            CollectionAssert.AreEqual(new[] { "Day10Setup.cs" }, callers, "NewScene yalnızca Editor kurulumunda olmalı");
            string setup = File.ReadAllText(Path.Combine(scripts, "App", "Day10Setup.cs"));
            StringAssert.StartsWith("#if UNITY_EDITOR", setup.TrimStart(), "tüm dosya Editor'a özel");
            StringAssert.Contains("isPlayingOrWillChangePlaymode", setup);
            Assert.Less(setup.IndexOf("if (!CanRun)", System.StringComparison.Ordinal), setup.IndexOf("CreateMainScene(catalog", System.StringComparison.Ordinal), "koruma sahne üretiminden önce");
        }

        [Test]
        public void TheRuntimeBootstrap_NeverCallsTheEditorSetup()
        {
            string scripts = Path.Combine(Directory.GetParent(ContentDir()).FullName, "Scripts");
            string bootstrap = File.ReadAllText(Path.Combine(scripts, "App", "GameBootstrap.cs"));

            StringAssert.DoesNotContain("Day10Setup", bootstrap);
            StringAssert.DoesNotContain("EditorSceneManager", bootstrap);
        }
    }
}
