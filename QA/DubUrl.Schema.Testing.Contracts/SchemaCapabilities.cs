namespace DubUrl.Schema.Testing.Contracts;

[Flags]
public enum SchemaCapabilities
{
    None = 0,
    CreateTable = 1,
    DropTableIfExists = 2,
}
