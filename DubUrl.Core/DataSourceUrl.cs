using DubUrl.Extensions;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl;

#if NET7_0_OR_GREATER
public class DataSourceUrl : BaseConnectionUrl
{
    public DataSourceUrl(string url, SchemeMapperBuilder? builder = null)
        : this(url, new Parser(), (builder ?? new()).Build()) { }

    internal DataSourceUrl(string url, IParser parser, SchemeMapper schemeMapper)
        : base(url, parser, schemeMapper) { }

    public virtual DbDataSource Create()
    {
        var providerFactory = GetProviderFactory();
        var dataSource = providerFactory.CreateDataSource(Parse());
        return dataSource;
    }
}
#endif
