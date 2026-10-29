using System.Data.Common;

namespace DubUrl;

/// <summary>Declares a verified native read-only access mode for the primary database file.</summary>
/// <remarks>This is not a transaction default, replica-routing hint, or a sandbox for arbitrary SQL.</remarks>
public sealed class ReadOnlyConnectionCapability(string keyword, string value, string dataSourceKeyword, string? verificationSql = null)
{
    internal void Apply(DbConnectionStringBuilder builder)
    {
        var source = Convert.ToString(builder[dataSourceKeyword], System.Globalization.CultureInfo.InvariantCulture) ?? "";
        if (string.IsNullOrWhiteSpace(source)
            || source.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith(":memory:?", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("file::memory:", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("md:", StringComparison.OrdinalIgnoreCase)
            || (source.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                && source.Split('?').Skip(1).SelectMany(query => query.Split('&'))
                    .Contains("mode=memory", StringComparer.OrdinalIgnoreCase)))
            throw new NotSupportedException("Read-only file access requires an existing persisted database; in-memory and remote DuckDB modes are unsupported.");
        builder[keyword] = value;
    }

    internal void Configure(DbConnection connection)
    {
        if (verificationSql is null) return;
        // A StateChange guard also covers callers opening/reopening the native connection
        // returned by Connect(), without replacing it with a wrapper.
        connection.StateChange += (_, change) =>
        {
            if (change.CurrentState != System.Data.ConnectionState.Open) return;
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = verificationSql;
                command.CommandTimeout = 5;
                if (!Convert.ToBoolean(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("The provider reused a writable database instance. Required read-only access could not be established.");
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        };
    }
}

/// <summary>Declares native file access mode on a specific driver route, without inheritance.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ReadOnlyConnectionAttribute(string keyword, string value, string dataSourceKeyword, string? verificationSql = null) : Attribute
{
    internal ReadOnlyConnectionCapability Create() => new(keyword, value, dataSourceKeyword, verificationSql);
}
