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
    ResolvedConnection Resolve(UrlInfo urlInfo)
    {
        var mapper = GetMapper(urlInfo.Schemes);
        mapper.Rewrite(urlInfo);
        return new(
            mapper.GetConnectionString(),
            mapper.GetDialect(),
            mapper.GetConnectivity(),
            mapper.GetParametrizer(),
            GetProviderFactory(urlInfo.Schemes)
        );
    }
    IMapper GetMapper(string alias);
    IMapper GetMapper(string[] aliases);
    DbProviderFactory GetProviderFactory(string[] aliases);
    bool CanHandle(string scheme);
}
