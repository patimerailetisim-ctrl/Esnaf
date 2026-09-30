using System;
using System.IO;
using Esnaf.Domain.Content;
using NUnit.Framework;

namespace Esnaf.Tests.Content
{
    public class ContentSourceTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "esnaf_content_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void DirectorySource_ReadsUtf8File()
        {
            File.WriteAllText(Path.Combine(_directory, "a.json"), "{ \"name\": \"Yıldız\" }", new System.Text.UTF8Encoding(false));
            var source = new DirectoryContentSource(_directory);

            string text;
            Assert.IsTrue(source.TryGetText("a.json", out text));
            Assert.AreEqual("{ \"name\": \"Yıldız\" }", text);
        }

        [Test]
        public void DirectorySource_MissingFile_ReturnsFalse()
        {
            var source = new DirectoryContentSource(_directory);

            string text;
            Assert.IsFalse(source.TryGetText("nope.json", out text));
            Assert.IsNull(text);
        }

        [Test]
        public void DirectorySource_MissingDirectory_ReturnsFalse()
        {
            var source = new DirectoryContentSource(Path.Combine(_directory, "does_not_exist"));

            string text;
            Assert.IsFalse(source.TryGetText("a.json", out text));
        }

        [TestCase("../a.json")]
        [TestCase("..\\a.json")]
        [TestCase("sub/a.json")]
        [TestCase("sub\\a.json")]
        [TestCase("a..json")]
        [TestCase("")]
        [TestCase(null)]
        public void DirectorySource_RejectsPathParts(string fileName)
        {
            var source = new DirectoryContentSource(_directory);

            string text;
            Assert.Throws<ArgumentException>(() => source.TryGetText(fileName, out text));
        }

        [TestCase("")]
        [TestCase(null)]
        public void DirectorySource_RequiresDirectory(string directory)
        {
            Assert.Throws<ArgumentException>(() => new DirectoryContentSource(directory));
        }

        [Test]
        public void DictionarySource_AddReadRemove()
        {
            var source = new DictionaryContentSource().Add("a.json", "x");

            string text;
            Assert.IsTrue(source.TryGetText("a.json", out text));
            Assert.AreEqual("x", text);
            Assert.IsFalse(source.TryGetText("b.json", out text));

            Assert.IsTrue(source.Remove("a.json"));
            Assert.IsFalse(source.TryGetText("a.json", out text));
            Assert.IsFalse(source.Remove("a.json"));
        }

        [Test]
        public void DictionarySource_NullTextBecomesEmpty_AndNameIsRequired()
        {
            var source = new DictionaryContentSource().Add("a.json", null);

            string text;
            Assert.IsTrue(source.TryGetText("a.json", out text));
            Assert.AreEqual(string.Empty, text);
            Assert.Throws<ArgumentException>(() => source.Add("", "x"));
        }
    }
}
