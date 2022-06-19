using DubUrl.Mapping;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public class UrlConnection
    {
        private MapperFactory MapperFactory { get; }
        private IMapper? Mapper { get; set; }
        private IParser Parser { get; }
        private string Url { get; }

        public UrlConnection(string url)
            : this(url, new Parser(), new MapperFactory()) { }

        public UrlConnection(string url, MapperFactory factory)
            : this(url, new Parser(), factory) { }

        internal UrlConnection(string url, IParser parser, MapperFactory factory)
            => (Url, Parser, MapperFactory) = (url, parser, factory);

        private (string ConnectionString, UrlInfo UrlInfo) ParseDetail()
        {
            var urlInfo = Parser.Parse(Url);
            Mapper = MapperFactory.Instantiate(urlInfo.Scheme);
            Mapper.Map(urlInfo);
            return (Mapper.GetConnectionString(), urlInfo);
        }

        public string Parse() => ParseDetail().ConnectionString;
    }
}
