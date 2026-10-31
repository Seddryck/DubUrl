using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;

internal static class TableDependencyPlanner
{
    public static Table[] Order(IEnumerable<Table> tables)
    {
        var remaining = tables.ToList();
        var known = remaining.Select(table => table.Identity.Key).ToHashSet(StringComparer.Ordinal);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<Table>();
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(table => table.Constraints.OfType<ForeignKeyConstraint>()
                .All(key => key.TargetTable.Key == table.Identity.Key || !known.Contains(key.TargetTable.Key) || emitted.Contains(key.TargetTable.Key)));
            if (next is null)
                throw new NotSupportedException($"Foreign-key dependency cycle cannot be created by this dialect: {string.Join(", ", remaining.Select(table => table.Identity))}. Remove the cycle.");
            remaining.Remove(next);
            emitted.Add(next.Identity.Key);
            ordered.Add(next);
        }
        return ordered.ToArray();
    }
}
