using System;
using System.Collections.Generic;

namespace Esnaf.Domain.Content
{
    /// <summary>Bellekteki metinlerden içerik kaynağı (testler için).</summary>
    public sealed class DictionaryContentSource : IContentSource
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        public DictionaryContentSource Add(string fileName, string text)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("File name is required.", nameof(fileName));
            }

            _files[fileName] = text ?? string.Empty;
            return this;
        }

        public bool Remove(string fileName)
        {
            return _files.Remove(fileName);
        }

        public bool TryGetText(string fileName, out string text)
        {
            return _files.TryGetValue(fileName, out text);
        }
    }
}
