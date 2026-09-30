using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// Golden fixture dosyalarını okumak için küçük JSON okuyucu (Unity test derlemesinde Newtonsoft yok).
    /// Nesne → Dictionary&lt;string, object&gt;, dizi → List&lt;object&gt;, sayı → double, metin → string, true/false → bool, null → null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = ReadValue(text, ref i);
            SkipWhite(text, ref i);
            if (i != text.Length)
            {
                throw new FormatException("Unexpected content at " + i);
            }

            return value;
        }

        private static void SkipWhite(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipWhite(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var obj = new Dictionary<string, object>();
                i++;
                SkipWhite(s, ref i);
                if (s[i] == '}')
                {
                    i++;
                    return obj;
                }

                while (true)
                {
                    SkipWhite(s, ref i);
                    string key = ReadString(s, ref i);
                    SkipWhite(s, ref i);
                    Expect(s, ref i, ':');
                    obj[key] = ReadValue(s, ref i);
                    SkipWhite(s, ref i);
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    Expect(s, ref i, '}');
                    return obj;
                }
            }

            if (c == '[')
            {
                var list = new List<object>();
                i++;
                SkipWhite(s, ref i);
                if (s[i] == ']')
                {
                    i++;
                    return list;
                }

                while (true)
                {
                    list.Add(ReadValue(s, ref i));
                    SkipWhite(s, ref i);
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    Expect(s, ref i, ']');
                    return list;
                }
            }

            if (c == '"')
            {
                return ReadString(s, ref i);
            }

            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0)
            {
                i += 4;
                return true;
            }

            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0)
            {
                i += 5;
                return false;
            }

            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0)
            {
                i += 4;
                return null;
            }

            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
            {
                i++;
            }

            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                }
                else
                {
                    sb.Append(s[i]);
                }

                i++;
            }

            i++;
            return sb.ToString();
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (s[i] != c)
            {
                throw new FormatException("Expected '" + c + "' at " + i);
            }

            i++;
        }
    }
}
