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
    /// Art/Customers klasörünü ve kaydedilmiş CustomerPortraitCatalog.asset'i (YAML) dosya düzeyinde doğrular. Unity'siz çalışır.
    /// Klasör (ya da katalog) henüz depoda yoksa testler Ignore olur: önce PNG'ler + "Esnaf > Setup Customer Portraits" ile oluşan katalog commit edilmelidir.
    /// </summary>
    public class CustomerPortraitFileTests
    {
        private static string Dir()
        {
            string project = Directory.GetParent(Directory.GetParent(TestPaths.ContentDataDirectory()).FullName).FullName;
            return Path.Combine(project, "Art", "Customers");
        }

        private static string[] Pngs()
        {
            if (!Directory.Exists(Dir()))
            {
                Assert.Ignore("Assets/_Project/Art/Customers depoda yok (portreler henüz commit edilmedi).");
            }

            return Directory.GetFiles(Dir(), "*.png");
        }

        private static string Nfc(string text)
        {
            return text.Normalize(System.Text.NormalizationForm.FormC);
        }

        /// <summary>
        /// Unity, ASCII olmayan metni YAML'de ÇİFT TIRNAKLI ve \uXXXX kaçışlı yazar (ör. _name: "O\u011Fuz"). Okurken tırnak ve kaçış çözülür;
        /// aksi halde Oğuz/Yiğit/İrem "ilk bakışta aynı" görünse de eşleşmez.
        /// </summary>
        private static string YamlString(string value)
        {
            string v = value.Trim();
            if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"')
            {
                v = v.Substring(1, v.Length - 2);
                v = Regex.Replace(v, @"\\u([0-9a-fA-F]{4})", m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
                v = v.Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            else if (v.Length >= 2 && v[0] == '\'' && v[v.Length - 1] == '\'')
            {
                v = v.Substring(1, v.Length - 2).Replace("''", "'");
            }

            return Nfc(v);
        }

        private static string CatalogText()
        {
            string path = Path.Combine(Dir(), "CustomerPortraitCatalog.asset");
            if (!File.Exists(path))
            {
                Assert.Ignore("CustomerPortraitCatalog.asset depoda yok (Unity'de Esnaf > Setup Customer Portraits çalıştırıp commit edin).");
            }

            return File.ReadAllText(path);
        }

        [TestCase("Berk", "Berk")]
        [TestCase("\"O\\u011Fuz\"", "O\u011Fuz")]
        [TestCase("\"Yi\\u011Fit\"", "Yi\u011Fit")]
        [TestCase("\"\\u0130rem\"", "\u0130rem")]
        public void UnityYamlStrings_AreDecodedBeforeComparing(string yaml, string expected)
        {
            Assert.AreEqual(expected, YamlString(yaml));
        }

        [Test]
        public void TheFolder_HoldsExactlyTheTwentyEightExpectedPortraits()
        {
            string[] names = Pngs().Select(p => Nfc(Path.GetFileNameWithoutExtension(p))).ToArray();

            CollectionAssert.AreEquivalent(CustomerPortraitNaming.ExpectedNames.Select(Nfc).ToArray(), names);
        }

        [Test]
        public void EveryPortraitPng_IsImportedAsAUiSprite_WithTransparencyAndNoMipmaps()
        {
            foreach (string png in Pngs())
            {
                string meta = png + ".meta";
                Assert.IsTrue(File.Exists(meta), Path.GetFileName(png) + ": .meta yok");
                string text = File.ReadAllText(meta);
                StringAssert.Contains("textureType: 8", text, png);
                StringAssert.Contains("spriteMode: 1", text, png);
                StringAssert.Contains("alphaIsTransparency: 1", text, png);
                StringAssert.Contains("enableMipMap: 0", text, png);
            }
        }

        [Test]
        public void TheCatalog_LinksEveryPortraitFileToItsOwnSprite_AllDistinct()
        {
            string[] pngs = Pngs();
            string catalog = CatalogText();

            var guidToName = new Dictionary<string, string>();
            foreach (string png in pngs)
            {
                string guid = Regex.Match(File.ReadAllText(png + ".meta"), @"^guid: (\w+)", RegexOptions.Multiline).Groups[1].Value;
                guidToName[guid] = Nfc(Path.GetFileNameWithoutExtension(png));
            }

            var linked = new Dictionary<string, string>(); // ad -> guid
            string currentName = null;
            foreach (string line in catalog.Split('\n'))
            {
                Match name = Regex.Match(line, @"^\s*- _name: (.+?)\s*$");
                if (name.Success)
                {
                    currentName = YamlString(name.Groups[1].Value);
                    continue;
                }

                Match sprite = Regex.Match(line, @"^\s+_sprite: \{fileID: 21300000, guid: (\w+), type: 3\}");
                if (sprite.Success && currentName != null)
                {
                    linked[currentName] = sprite.Groups[1].Value;
                }
            }

            CollectionAssert.AreEquivalent(CustomerPortraitNaming.ExpectedNames.Select(Nfc).ToArray(), linked.Keys.ToArray(), "28 portre katalogda");
            foreach (KeyValuePair<string, string> pair in linked)
            {
                Assert.IsTrue(guidToName.ContainsKey(pair.Value), pair.Key + ": Sprite GUID'i hiçbir PNG'ye ait değil / boş");
                Assert.AreEqual(pair.Key, guidToName[pair.Value], pair.Key + ": yanlış portreye bağlı");
            }

            Assert.AreEqual(28, linked.Values.Distinct().Count(), "iki portre aynı Sprite'a bağlı");
        }
    }
}
