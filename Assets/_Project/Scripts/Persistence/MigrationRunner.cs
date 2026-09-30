using System;
using System.Collections.Generic;
using Esnaf.Core;
using Newtonsoft.Json.Linq;

namespace Esnaf.Persistence
{
    /// <summary>
    /// Migrasyon zinciri (GDD v0.3 6.6): eski sürümlü yükü adım adım güncel sürüme taşır. Zincir kurulurken doğrulanır
    /// (adımlar <c>To = From + 1</c>, <c>From</c> benzersiz, güncel sürümü aşmaz).
    /// </summary>
    public sealed class MigrationRunner
    {
        private readonly Dictionary<int, IMigration> _byFrom = new Dictionary<int, IMigration>();

        public int CurrentVersion { get; }

        public MigrationRunner(int currentVersion, IEnumerable<IMigration> migrations)
        {
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }

            if (currentVersion < 1)
            {
                throw new ArgumentException("The current save version must be at least 1.", nameof(currentVersion));
            }

            CurrentVersion = currentVersion;
            foreach (IMigration m in migrations)
            {
                if (m == null)
                {
                    throw new ArgumentNullException(nameof(migrations), "A migration is null.");
                }

                if (m.From < 0 || m.To != m.From + 1 || m.To > currentVersion)
                {
                    throw new ArgumentException("Migration " + m.From + "→" + m.To + " must go exactly one version up and stay within 0.." + currentVersion + ".", nameof(migrations));
                }

                if (_byFrom.ContainsKey(m.From))
                {
                    throw new ArgumentException("Two migrations start at version " + m.From + ".", nameof(migrations));
                }

                _byFrom.Add(m.From, m);
            }
        }

        /// <summary>
        /// Yükü <paramref name="fromVersion"/>'dan güncel sürüme taşır. Güncel sürümde hiçbir şey yapmaz; daha yeni sürüm
        /// <c>save.version_newer</c>, yolu olmayan eski sürüm <c>save.version_unsupported</c>, adım hatası <c>save.migration</c> döner.
        /// </summary>
        public Result Migrate(JObject payload, int fromVersion)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (fromVersion > CurrentVersion)
            {
                return Result.Fail("save.version_newer", "The save version " + fromVersion + " is newer than the supported " + CurrentVersion + ".");
            }

            for (int version = fromVersion; version < CurrentVersion; version++)
            {
                IMigration step;
                if (version < 0 || !_byFrom.TryGetValue(version, out step))
                {
                    return Result.Fail("save.version_unsupported", "There is no migration from save version " + version + ".");
                }

                try
                {
                    step.Apply(payload);
                }
                catch (Exception ex)
                {
                    return Result.Fail("save.migration", "Migration " + step.From + "→" + step.To + " failed: " + ex.Message);
                }
            }

            return Result.Ok();
        }
    }
}
