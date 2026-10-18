using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Conversion;

/// <summary>Optional provider capability for translating a connection string into a URI.</summary>
public interface IReverseConnectionMapper
{
    bool SupportsReverseConversion => true;
    ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder connectionString);
}
