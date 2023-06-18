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

namespace DubUrl
{
    public class ConnectionUrl
    {
        private record ParseResult(string ConnectionString, UrlInfo UrlInfo, IDialect Dialect, IConnectivity Connectivity, IParametrizer Parametrizer) { }
        private ParseResult? _result;
        private ParseResult Result { get => _result ??= ParseDetail(); }
        private SchemeMapperBuilder SchemeMapperBuilder { get; }
        private IMapper? Mapper { get; set; }
        private IParser Parser { get; }
        
        private string Url { get; }

        public ConnectionUrl(string url)
            : this(url, new Parser(), new SchemeMapperBuilder()) { }

        public ConnectionUrl(string url, SchemeMapperBuilder builder)
            : this(url, new Parser(), builder) { }

        internal ConnectionUrl(string url, IParser parser, SchemeMapperBuilder builder)
            => (Url, Parser, SchemeMapperBuilder) = (url, parser, builder);

        private ParseResult ParseDetail()
        {
            var urlInfo = Parser.Parse(Url);
            SchemeMapperBuilder.Build();
            Mapper = SchemeMapperBuilder.GetMapper(urlInfo.Schemes);
            Mapper.Rewrite(urlInfo);
            return new ParseResult(Mapper.GetConnectionString(), urlInfo, Mapper.GetDialect(), Mapper.GetConnectivity(), Mapper.GetParametrizer());
        }

        public string Parse() => Result.ConnectionString;

        public virtual IDbConnection Connect()
        {
            var provider = SchemeMapperBuilder.GetProviderFactory(Result.UrlInfo.Schemes);
            var connection = provider.CreateConnection() ?? throw new ArgumentNullException();
            connection.ConnectionString = Result.ConnectionString;
            return connection;
        }

        public virtual IDbConnection Open()
        {
            var connection = Connect();
            connection.Open();
            return connection;
        }
                    
        public virtual IDialect Dialect { get => Result.Dialect; }
        public virtual IConnectivity Connectivity { get => Result.Connectivity; }
        public virtual IParametrizer Parametrizer { get => Result.Parametrizer; }
    }
}
