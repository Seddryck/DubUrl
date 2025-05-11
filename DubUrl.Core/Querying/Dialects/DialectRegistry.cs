using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

public class DialectRegistry : IDialectRegistry
{
    private readonly Dictionary<Type, List<string>> _aliases;
    private readonly Dictionary<Type, IDialect> _dialects;

    public DialectRegistry(Dictionary<Type, IDialect> dialects, Dictionary<Type, List<string>>? aliases = null)
        => (_dialects, _aliases) = (dialects, aliases ?? []);

    public T Get<T>() where T : IDialect
        => (T)Get(typeof(T));

    public IDialect Get(Type dialectType)
    {
        if (!_dialects.TryGetValue(dialectType, out var value))
            throw new DialectNotFoundException(dialectType, [.. _dialects.Keys]);
        return value;
    }

    public IDialect Get(string scheme)
        => Get(_aliases.FirstOrDefault(x => x.Value.Contains(scheme)).Key
                ?? throw new DialectNotFoundException(scheme, [.. _dialects.Keys])
           );
}
