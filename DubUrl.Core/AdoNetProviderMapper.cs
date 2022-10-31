using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public class AdoNetProviderMapper : BaseMapper
    {
        public AdoNetProviderMapper(IConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
            : base(rewriter, dialect, new NativeConnectivity(), parametrizer)
        { }
    }
}
