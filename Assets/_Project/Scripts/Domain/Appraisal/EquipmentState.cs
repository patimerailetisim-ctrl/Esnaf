using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Dükkânın sahip olduğu ekipman kümesi (S3 için "test_device"). Yalnızca durumdur: satın alma akışı (12.000 TL yatırım) sonraki
    /// günlerin işidir; şimdilik <see cref="Grant"/> ile verilir.
    /// </summary>
    public sealed class EquipmentState
    {
        private readonly SortedSet<string> _owned = new SortedSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<string> All
        {
            get { return new ReadOnlyCollection<string>(_owned.ToList()); }
        }

        public bool Owns(string equipmentId)
        {
            return equipmentId != null && _owned.Contains(equipmentId);
        }

        public void Grant(string equipmentId)
        {
            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                throw new ArgumentException("Equipment id is required.", nameof(equipmentId));
            }

            _owned.Add(equipmentId);
        }
    }
}
