using System;
using System.IO;
using System.Text;

namespace Esnaf.Domain.Content
{
    /// <summary>
    /// Bir klasördeki JSON dosyalarını okur. Simülatör, editör doğrulama menüsü ve testler kullanır
    /// (Unity oyunu ise TextAsset kaynağını kullanır).
    /// </summary>
    public sealed class DirectoryContentSource : IContentSource
    {
        private readonly string _directory;

        public DirectoryContentSource(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Directory is required.", nameof(directory));
            }

            _directory = directory;
        }

        public bool TryGetText(string fileName, out string text)
        {
            if (string.IsNullOrEmpty(fileName)
                || fileName.IndexOf('/') >= 0
                || fileName.IndexOf('\\') >= 0
                || fileName.Contains(".."))
            {
                throw new ArgumentException("File name must be a plain file name without path parts.", nameof(fileName));
            }

            string path = Path.Combine(_directory, fileName);
            if (!File.Exists(path))
            {
                text = null;
                return false;
            }

            text = File.ReadAllText(path, Encoding.UTF8);
            return true;
        }
    }
}
