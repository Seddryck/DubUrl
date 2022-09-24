﻿using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public interface IMapper
    {
        IReadOnlyDictionary<string, object> Rewrite(UrlInfo urlInfo);
        string GetConnectionString();
        IDialect GetDialect();
        string GetProviderName();
        IParametrizer GetParametrizer();
    }

    public interface IOdbcMapper : IMapper { }
    public interface IOleDbMapper : IMapper { }
}