using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;

namespace DubUrl.Rewriting
{
    public interface IOdbcConnectionStringRewriter : IConnectionStringRewriter
    { }

    public interface IOleDbConnectionStringRewriter : IConnectionStringRewriter
    { }
}
