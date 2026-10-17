using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class Schema
{
    public OrderedImmutableDictionary<string, Table> Tables { get; }
    public OrderedImmutableDictionary<string, Index> Indexes { get; }
    public OrderedImmutableDictionary<string, View> Views { get; }

    public Schema(Table[] tables)
        : this(tables, [], [])
    { }

    public Schema(Table[] tables, Index[] indexes)
        : this(tables, indexes, [])
    { }

    public Schema(Table[] tables, Index[] indexes, View[] views)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(indexes);
        ArgumentNullException.ThrowIfNull(views);

        // Check for duplicate table names
        var duplicates = tables.GroupBy(t => t.Identity).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0)
            throw new ArgumentException($"Duplicate table names found: {string.Join(", ", duplicates.Select(identity => identity.ToString()))}", nameof(tables));

        Tables = OrderedImmutableDictionary<string, Table>.From(
            tables.Select(t => new KeyValuePair<string, Table>(t.Identity.Key, t)));

        foreach (var table in tables)
        foreach (var foreignKey in table.Constraints.OfType<Constraints.ForeignKeyConstraint>())
        {
            if (!Tables.TryGetValue(foreignKey.TargetTable.Key, out var targetTable))
                throw new ArgumentException(
                    $"Foreign key '{foreignKey.Name}' on table '{table.Name}' references missing table '{foreignKey.TargetTableName}'.",
                    nameof(tables));
            var missingColumns = foreignKey.TargetColumns.Where(name => !targetTable.Columns.ContainsKey(name)).ToArray();
            if (missingColumns.Length > 0)
                throw new ArgumentException(
                    $"Foreign key '{foreignKey.Name}' references missing target column(s) {string.Join(", ", missingColumns)} on table '{targetTable.Name}'.",
                    nameof(tables));
        }

        // Check for duplicate index names
        var duplicatedIndexes = indexes.GroupBy(t => t.Identity).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicatedIndexes.Length > 0)
            throw new ArgumentException($"Duplicate index names found: {string.Join(", ", duplicatedIndexes.Select(identity => identity.ToString()))}", nameof(indexes));

        Indexes = OrderedImmutableDictionary<string, Index>.From(
            indexes.Select(t => new KeyValuePair<string, Index>(t.Identity.Key, t)));

        foreach (var index in indexes)
        {
            if (!Tables.ContainsKey(index.TableIdentity.Key))
                throw new ArgumentException(
                    $"Index '{index.Identity}' references missing table '{index.TableIdentity}'.",
                    nameof(indexes));
            var table = Tables[index.TableIdentity.Key];
            var missingColumns = index.Columns.Keys.Where(column => !table.Columns.ContainsKey(column)).ToArray();
            if (missingColumns.Length > 0)
                throw new ArgumentException(
                    $"Index '{index.Identity}' references missing column(s) {string.Join(", ", missingColumns)} on table '{table.Identity}'.",
                    nameof(indexes));
        }

        var duplicatedViews = views.GroupBy(view => view.Identity).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToArray();
        if (duplicatedViews.Length > 0)
            throw new ArgumentException(
                $"Duplicate view names found: {string.Join(", ", duplicatedViews.Select(identity => identity.ToString()))}",
                nameof(views));

        var tableViewCollisions = views.Where(view => Tables.ContainsKey(view.Identity.Key))
            .Select(view => view.Identity).ToArray();
        if (tableViewCollisions.Length > 0)
            throw new ArgumentException(
                $"Table and view identities must be unique: {string.Join(", ", tableViewCollisions.Select(identity => identity.ToString()))}",
                nameof(views));

        Views = OrderedImmutableDictionary<string, View>.From(
            views.Select(view => new KeyValuePair<string, View>(view.Identity.Key, view)));

        foreach (var view in views)
            if (!Tables.ContainsKey(view.Query.Source.Key) && !Views.ContainsKey(view.Query.Source.Key))
                throw new ArgumentException(
                    $"View '{view.Identity}' references missing source relation '{view.Query.Source}'.",
                    nameof(views));

        _ = ViewDependencyPlanner.Order(Views.Values);
    }

    public Table GetTable(DatabaseObjectName identity)
        => Tables[identity?.Key ?? throw new ArgumentNullException(nameof(identity))];

    public Index GetIndex(DatabaseObjectName identity)
        => Indexes[identity?.Key ?? throw new ArgumentNullException(nameof(identity))];

    public View GetView(DatabaseObjectName identity)
        => Views[identity?.Key ?? throw new ArgumentNullException(nameof(identity))];
}
