namespace DubUrl.Querying.Dialects.Formatters;

/// <summary>Indicates that a dialect rejected an identifier during rendering.</summary>
public sealed class InvalidIdentifierException(string message) : DubUrlException(message)
{
}
