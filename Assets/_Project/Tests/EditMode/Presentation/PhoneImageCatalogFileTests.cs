using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Esnaf.Presentation;
using Esnaf.Tests.Support;
using NUnit.Framework;

namespace Esnaf.Tests.Presentation
{
    /// <summary>
    /// Kaydedilmiş PhoneImageCatalog.asset'i (YAML) ve PNG .meta dosyalarını dosya düzeyinde doğrular: 10 model x 4 açı = 40 bağlantı doğru dosyaya gider.
    /// Unity'siz çalışır (dotnet test ve Test Runner aynı dosyayı koşar); Sprite'ın çalışma anında yüklenmesini Unity-only testler denetler.
    /// </summary>
    public class PhoneImageCatalogFileTests
    {
        private static readonly string[] Kinds = { "Front", "Back", "Side", "Camera" };
        private static readonly string[] Fields = { "_front", "_back", "_side", "_camera" };

        private static string ArtDir()
        {
            string project = Directory.GetParent(Directory.GetParent(TestPaths.ContentDataDirectory()).FullName).FullName;
            return Path.Combine(project, "Art", "Phones");
        }

        private static string BaseName(string id)
        {
            return string.Concat(id.Substring("phone.".Length).Split('_').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }

        // guid -> png yolu
        private static Dictionary<string, string> PngByGuid()
        {
            var map = new Dictionary<string, string>();
            foreach (string meta in Directory.GetFiles(ArtDir(), "*.png.meta", SearchOption.AllDirectories))
            {
                string guid = Regex.Match(File.ReadAllText(meta), @"^guid: (\w+)", RegexOptions.Multiline).Groups[1].Value;
                map.Add(guid, meta.Substring(0, meta.Length - ".meta".Length));
            }

            return map;
        }

        private sealed class Entry
        {
            public string Id;
            public readonly Dictionary<string, string> Guids = new Dictionary<string, string>();
        }

        private static List<Entry> ReadCatalog()
        {
            var entries = new List<Entry>();
            Entry current = null;
            foreach (string line in File.ReadAllLines(Path.Combine(ArtDir(), "PhoneImageCatalog.asset")))
            {
                Match id = Regex.Match(line, @"^\s*- _definitionId: (\S+)");
                if (id.Success)
                {
                    current = new Entry { Id = id.Groups[1].Value };
                    entries.Add(current);
                    continue;
                }

                Match field = Regex.Match(line, @"^\s+(_\w+): \{fileID: (\d+), guid: (\w+), type: 3\}");
                if (field.Success && current != null)
                {
                    Assert.AreEqual("21300000", field.Groups[2].Value, current.Id + " " + field.Groups[1].Value + ": tek Sprite alt-varlığı değil");
                    current.Guids[field.Groups[1].Value] = field.Groups[3].Value;
                }
            }

            return entries;
        }

        [Test]
        public void TheCatalog_HasExactlyTheTenModelsOfTheContent()
        {
            CollectionAssert.AreEquivalent(PhoneModelIds.All.ToArray(), ReadCatalog().Select(e => e.Id).ToArray());
        }

        [Test]
        public void Every_ModelAndAngle_PointsAtItsOwnPng_FortyLinks()
        {
            Dictionary<string, string> pngs = PngByGuid();
            int links = 0;

            foreach (Entry entry in ReadCatalog())
            {
                string baseName = BaseName(entry.Id);
                for (int i = 0; i < Kinds.Length; i++)
                {
                    string guid;
                    Assert.IsTrue(entry.Guids.TryGetValue(Fields[i], out guid), entry.Id + " + " + Kinds[i] + ": Sprite boş");
                    string path;
                    Assert.IsTrue(pngs.TryGetValue(guid, out path), entry.Id + " + " + Kinds[i] + ": guid hiçbir PNG'ye ait değil");
                    Assert.AreEqual(baseName + "_" + Kinds[i] + ".png", Path.GetFileName(path), entry.Id + " + " + Kinds[i]);
                    Assert.AreEqual(baseName, new DirectoryInfo(Path.GetDirectoryName(path)).Name, entry.Id + " + " + Kinds[i] + ": model klasörü");
                    links++;
                }
            }

            Assert.AreEqual(40, links);
        }

        [Test]
        public void AllViews_IsOnlyAReference_AndNeverUsedForAnAngle()
        {
            Dictionary<string, string> pngs = PngByGuid();

            foreach (Entry entry in ReadCatalog())
            {
                string allViews;
                Assert.IsTrue(entry.Guids.TryGetValue("_allViews", out allViews), entry.Id);
                Assert.AreEqual(BaseName(entry.Id) + "_AllViews.png", Path.GetFileName(pngs[allViews]), entry.Id);

                var angleGuids = Fields.Select(f => entry.Guids[f]).ToList();
                CollectionAssert.DoesNotContain(angleGuids, allViews, entry.Id);
                Assert.AreEqual(4, angleGuids.Distinct().Count(), entry.Id + ": aynı Sprite birden çok açıda");
            }
        }

        [Test]
        public void EveryPng_IsImportedAsASingleUiSprite_WithTransparencyAndNoMipmaps()
        {
            foreach (string meta in Directory.GetFiles(ArtDir(), "*.png.meta", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(meta);
                string name = Path.GetFileName(meta);
                StringAssert.Contains("textureType: 8", text, name);
                StringAssert.Contains("spriteMode: 1", text, name);
                StringAssert.Contains("alphaIsTransparency: 1", text, name);
                StringAssert.Contains("enableMipMap: 0", text, name);
            }
        }

        [Test]
        public void TheFourAngleImages_AreRealRgbaPngs_WithAnAlphaChannel()
        {
            Dictionary<string, string> pngs = PngByGuid();

            foreach (Entry entry in ReadCatalog())
            {
                foreach (string field in Fields)
                {
                    byte[] header = new byte[26];
                    using (FileStream stream = File.OpenRead(pngs[entry.Guids[field]]))
                    {
                        Assert.AreEqual(26, stream.Read(header, 0, 26));
                    }

                    Assert.AreEqual(0x89, header[0], entry.Id + " " + field);
                    Assert.AreEqual((byte)'P', header[1], entry.Id + " " + field);
                    Assert.AreEqual(6, header[25], entry.Id + " " + field + ": PNG RGBA değil (şeffaflık yok)");
                }
            }
        }

        [Test]
        public void TheCatalogAsset_UsesThePhoneImageCatalogScript()
        {
            string scriptMeta = Path.Combine(
                Directory.GetParent(Directory.GetParent(TestPaths.ContentDataDirectory()).FullName).FullName, "Scripts", "App", "Ui", "PhoneImageCatalog.cs.meta");
            string scriptGuid = Regex.Match(File.ReadAllText(scriptMeta), @"^guid: (\w+)", RegexOptions.Multiline).Groups[1].Value;

            StringAssert.Contains("guid: " + scriptGuid, File.ReadAllText(Path.Combine(ArtDir(), "PhoneImageCatalog.asset")));
        }
    }
}
