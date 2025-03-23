namespace DubUrl.Schema;
/// <summary>
/// Specifies options for schema creation operations.
/// </summary>
public enum SchemaCreationOptions
{
    /// <summary>
    /// No special options for schema creation.
    /// </summary>
    None = 0,
    /// <summary>
    /// Drop tables if they already exist before creating them.
    /// </summary>
    DropIfExists = 1,
}
