using DubUrl.Adomd.Discovery;
using DubUrl.Parsing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Implementation;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace DubUrl.Adomd.Rewriting;

internal class SsasMultidimRewriter : SsasRewriter
{
    public SsasMultidimRewriter(DbConnectionStringBuilder csb)
        : base(csb)
    { }
}
