using System;
using System.Collections.Generic;
using Esnaf.Domain.Content;
using UnityEngine;

namespace Esnaf.Content
{
    /// <summary>
    /// Unity TextAsset'lerini Domain'in <see cref="IContentSource"/> arayüzüne bağlar (yalnızca metin köprüsü).
    /// Asset adı + ".json" dosya adı sayılır: TextAsset "phone_models" -> "phone_models.json".
    /// Ayrıştırma ve doğrulama Domain'dedir (ContentDatabase.Load).
    /// </summary>
    public sealed class TextAssetContentSource : IContentSource
    {
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);

        public TextAssetContentSource(IEnumerable<TextAsset> assets)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            foreach (TextAsset asset in assets)
            {
                if (asset == null)
                {
                    continue;
                }

                _texts[asset.name + ".json"] = asset.text;
            }
        }

        public bool TryGetText(string fileName, out string text)
        {
            return _texts.TryGetValue(fileName, out text);
        }
    }
}
