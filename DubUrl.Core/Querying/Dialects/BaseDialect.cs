using DubUrl.Mapping;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

public abstract class BaseDialect : IDialect
{
    public IRenderer Renderer { get; }
    public ICaster[] Casters { get; }
    public IDbTypeMapper DbTypeMapper { get; }
    public ISqlFunctionMapper SqlFunctionMapper { get; }
    public virtual string[] Aliases { get; }
    public virtual ILanguage Language { get; }

    protected BaseDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper, ISqlFunctionMapper? sqlFunctionMapper = null)
        => (Language, Aliases, Renderer, Casters, DbTypeMapper, SqlFunctionMapper) = (language, aliases, renderer, casters, dbTypeMapper, sqlFunctionMapper ?? AnsiFunctionMapper.Instance);

    protected static DialectBuilder DialectBuilder => _dialectBuilder ??= CreateDialectBuilder();
    private static DialectBuilder? _dialectBuilder;
    private static DialectBuilder CreateDialectBuilder()
    {
        var builder = new DialectBuilder();
        var introspectors = new BaseMapperIntrospector[] { new NativeMapperIntrospector(), new WrapperMapperIntrospector() };
        builder.AddAliases(typeof(AnsiDialect), ["ansi"]);
        foreach (var mapperData in introspectors.Aggregate(
                Array.Empty<MapperInfo>(), (data, introspector)
                => [.. data, .. introspector.Locate()]))
        {
            builder.AddAliases(mapperData.DialectType, mapperData.Aliases);
        }
        builder.Build();
        return builder;
    }
}
