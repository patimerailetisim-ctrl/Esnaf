using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esnaf.Persistence;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// Bellekte kayıt klasörü + hata/kesinti enjeksiyonu (atomiklik ve bozuk dosya testleri için).
    /// <c>Log</c> işlemleri sırasıyla tutar ("write:slot0.tmp", "copy:slot0.json>slot0.bak1.json"...).
    /// </summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _failures = new List<string>();
        private readonly Dictionary<string, Func<string, string>> _readTransforms = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal);
        private string _truncateWriteOf;

        public List<string> Log { get; } = new List<string>();

        public IReadOnlyDictionary<string, string> Files
        {
            get { return _files; }
        }

        /// <summary>"op:ad" kalıbındaki bir işlem IOException ile başarısız olur (op: write, read, copy, replace, delete; ad: alt dize).</summary>
        public void FailOn(string op, string nameContains)
        {
            _failures.Add(op + ":" + nameContains);
        }

        /// <summary>Adı bu metni içeren dosyanın YAZILMASI yarıda kesilir (yarım içerik yazılır, sonra IOException).</summary>
        public void InterruptWriteOf(string nameContains)
        {
            _truncateWriteOf = nameContains;
        }

        /// <summary>Bu adla okunan içerik dönüştürülür (örn. tmp geri okunurken bozulma).</summary>
        public void CorruptReadOf(string name, Func<string, string> transform)
        {
            _readTransforms[name] = transform;
        }

        public void ClearFaults()
        {
            _failures.Clear();
            _readTransforms.Clear();
            _truncateWriteOf = null;
        }

        public void Put(string name, string text)
        {
            _files[name] = text;
        }

        public bool Exists(string name)
        {
            return _files.ContainsKey(name);
        }

        public string ReadAllText(string name)
        {
            MaybeFail("read", name);
            string text;
            if (!_files.TryGetValue(name, out text))
            {
                throw new FileNotFoundException(name);
            }

            Func<string, string> transform;
            return _readTransforms.TryGetValue(name, out transform) ? transform(text) : text;
        }

        public void WriteAllText(string name, string text)
        {
            Log.Add("write:" + name);
            MaybeFail("write", name);
            if (_truncateWriteOf != null && name.Contains(_truncateWriteOf))
            {
                _files[name] = text.Substring(0, text.Length / 2);
                throw new IOException("Simulated interruption while writing " + name);
            }

            _files[name] = text;
        }

        public void Copy(string from, string to)
        {
            Log.Add("copy:" + from + ">" + to);
            MaybeFail("copy", from + ">" + to);
            _files[to] = _files[from];
        }

        public void Replace(string from, string to)
        {
            Log.Add("replace:" + from + ">" + to);
            MaybeFail("replace", from + ">" + to);
            _files[to] = _files[from];
            _files.Remove(from);
        }

        public void Delete(string name)
        {
            Log.Add("delete:" + name);
            MaybeFail("delete", name);
            _files.Remove(name);
        }

        public IReadOnlyList<string> List()
        {
            return _files.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        }

        private void MaybeFail(string op, string name)
        {
            foreach (string failure in _failures)
            {
                int colon = failure.IndexOf(':');
                if (failure.Substring(0, colon) == op && name.Contains(failure.Substring(colon + 1)))
                {
                    throw new IOException("Simulated " + op + " failure on " + name);
                }
            }
        }
    }

    /// <summary>Elle ilerletilen saat (yalnızca kayıt üst verisi ve .corrupt dosya adı için).</summary>
    public sealed class FakeSaveClock : ISaveClock
    {
        public DateTime UtcNow { get; set; }

        public FakeSaveClock(DateTime start)
        {
            UtcNow = start;
        }
    }
}
