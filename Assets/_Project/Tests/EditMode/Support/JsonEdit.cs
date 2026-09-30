using System;
using System.Text;
using NUnit.Framework;

namespace Esnaf.Tests.Support
{
    /// <summary>Test JSON'larında bir özelliğin DEĞERİNİ değiştirmek için küçük yardımcı (Unity testlerinde Newtonsoft yok).</summary>
    public static class JsonEdit
    {
        /// <summary>
        /// "property" adlı özelliğin değerini değiştirir. Arama, sırayla verilen çapaların (yol) sonundan başlar;
        /// böylece aynı adlı iç içe alanlar ayrı ayrı hedeflenir.
        /// </summary>
        public static string Set(string json, string property, string newValue, params string[] path)
        {
            int from = 0;
            foreach (string anchor in path)
            {
                int at = json.IndexOf(anchor, from, StringComparison.Ordinal);
                Assert.GreaterOrEqual(at, 0, "Çapa bulunamadı: " + anchor);
                from = at + anchor.Length;
            }

            string key = "\"" + property + "\":";
            int keyAt = json.IndexOf(key, from, StringComparison.Ordinal);
            Assert.GreaterOrEqual(keyAt, 0, "Özellik bulunamadı: " + property);
            int valueStart = keyAt + key.Length;
            while (json[valueStart] == ' ')
            {
                valueStart++;
            }

            int end = valueStart;
            char open = json[valueStart];
            if (open == '{' || open == '[')
            {
                int depth = 0;
                bool inString = false;
                for (; end < json.Length; end++)
                {
                    char c = json[end];
                    if (c == '"')
                    {
                        inString = !inString;
                    }
                    else if (!inString && (c == '{' || c == '['))
                    {
                        depth++;
                    }
                    else if (!inString && (c == '}' || c == ']'))
                    {
                        depth--;
                        if (depth == 0)
                        {
                            end++;
                            break;
                        }
                    }
                }
            }
            else if (open == '"')
            {
                end = json.IndexOf('"', valueStart + 1) + 1;
            }
            else
            {
                while (json[end] != ',' && json[end] != '}' && json[end] != ' ')
                {
                    end++;
                }
            }

            var sb = new StringBuilder();
            sb.Append(json, 0, valueStart).Append(newValue).Append(json, end, json.Length - end);
            return sb.ToString();
        }

        public static string Missing(string json, string property, params string[] path)
        {
            return Set(json, property, "null", path);
        }
    }
}
