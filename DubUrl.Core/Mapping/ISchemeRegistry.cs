using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Parsing;

namespace DubUrl.Mapping;
public interface ISchemeRegistry
{
    ResolvedConnection Resolve(ConnectionUri connectionUri);
    bool TryResolve(
        ConnectionUri connectionUri,
        out ResolvedConnection? resolvedConnection,
        out ConnectionResolutionDiagnostic? diagnostic);
    IMapper GetMapper(string alias);
    IMapper GetMapper(SchemeKey scheme);
    DbProviderFactory GetProviderFactory(SchemeKey scheme);
    bool CanHandle(string scheme);
}
