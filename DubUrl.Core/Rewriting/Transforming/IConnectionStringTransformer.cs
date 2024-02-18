using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.Rewriting.Transforming;
internal interface IConnectionStringTransformer
{
    ITokenMapper[] GetTokenMappers();
}
