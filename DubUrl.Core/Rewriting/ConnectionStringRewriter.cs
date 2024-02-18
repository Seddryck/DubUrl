using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting;

public class ConnectionStringRewriter : IConnectionStringRewriter
{
    private ISpecificator Specificator { get; }
    protected IEnumerable<ITokenMapper> TokenMappers { get; set; }
    public string ConnectionString
    { get => Specificator.ConnectionString; }

    public ConnectionStringRewriter(ISpecificator specificator, BaseTokenMapper[] tokenMappers)
        => (Specificator, TokenMappers) = (specificator, tokenMappers);

    public IReadOnlyDictionary<string, object> Execute(UrlInfo urlInfo)
    {
        foreach (var tokenMapper in TokenMappers)
        {
            tokenMapper.Accept(Specificator);
            tokenMapper.Execute(urlInfo);
        }
        return Specificator.ToReadOnlyDictionary();
    }

    protected void Replace(Type oldMapperType, BaseTokenMapper newMapper)
    {
        var tokenMappers = TokenMappers.ToArray();
        for (int i = 0; i < tokenMappers.Length; i++)
        {
            if (tokenMappers[i].GetType() == oldMapperType)
                tokenMappers[i] = newMapper;
        }
        TokenMappers = tokenMappers;
    }

    public void Prepend(ITokenMapper mapper)
        => TokenMappers = TokenMappers.Prepend(mapper);
    public void Append(ITokenMapper mapper)
        => TokenMappers = TokenMappers.Append(mapper);
}
