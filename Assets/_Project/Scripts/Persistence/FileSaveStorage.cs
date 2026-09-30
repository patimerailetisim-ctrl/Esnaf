using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Esnaf.Persistence
{
    /// <summary><see cref="ISaveStorage"/>'ın gerçek dosya sistemi karşılığı. Klasör ilk yazmada oluşturulur.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _directory;

        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A save directory is required.", nameof(directory));
            }

            _directory = directory;
        }

        public bool Exists(string name)
        {
            return File.Exists(PathOf(name));
        }

        public string ReadAllText(string name)
        {
            return File.ReadAllText(PathOf(name), Utf8NoBom);
        }

        public void WriteAllText(string name, string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Directory.CreateDirectory(_directory);
            byte[] bytes = Utf8NoBom.GetBytes(text);
            using (var stream = new FileStream(PathOf(name), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true); // işletim sistemi önbelleğinden diske
            }
        }

        public void Copy(string from, string to)
        {
            File.Copy(PathOf(from), PathOf(to), true);
        }

        public void Replace(string from, string to)
        {
            string source = PathOf(from);
            string target = PathOf(to);
            if (File.Exists(target))
            {
                File.Replace(source, target, null);
            }
            else
            {
                File.Move(source, target);
            }
        }

        public void Delete(string name)
        {
            string path = PathOf(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public IReadOnlyList<string> List()
        {
            var names = new List<string>();
            if (Directory.Exists(_directory))
            {
                foreach (string path in Directory.GetFiles(_directory))
                {
                    names.Add(Path.GetFileName(path));
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private string PathOf(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 || name == "." || name == "..")
            {
                throw new ArgumentException("A save file name must be a plain file name: '" + name + "'.", nameof(name));
            }

            return Path.Combine(_directory, name);
        }
    }
}
