using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;
public class SchemeNormalizer(IReadOnlyDictionary<string, ISchemeHandler> schemeHandlers)
{
    protected readonly char[] Separators = ['+', ':'];
    protected IReadOnlyDictionary<string, ISchemeHandler> SchemeHandlers { get; } = schemeHandlers;

    public string Normalize(string scheme)
        => Normalize(scheme.Split(Separators));

    public string Normalize(string[] schemes)
    {
        var schemeHandlers = SchemeHandlers.Where(x => schemes.Contains(x.Key));
        var handlerSchemes = schemeHandlers.Aggregate(Array.Empty<string>(), (data, handler)
                => [.. data, .. handler.Value.Schemes]);
        if (schemes.Length - schemeHandlers.Count() != 1)
            throw new ArgumentOutOfRangeException(nameof(schemes));

        var databaseAlias = schemes.Single(x => !handlerSchemes.Contains(x));
        var wrapperHandler = schemeHandlers.SingleOrDefault(x => x.Value is IWrapperConnectivity);

        var normalized = wrapperHandler.Value is null ? databaseAlias : $"{wrapperHandler.Value.Schemes[0]}+{databaseAlias}";

        var remainingHandlers = schemeHandlers.Where(x => x.Value is not IWrapperConnectivity).OrderBy(x => x.GetHashCode());
        foreach (var handler in remainingHandlers)
            normalized += $"+{handler.Value.Schemes[0]}";

        return normalized;
    }
}
