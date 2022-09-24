using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    public interface ITokenMapper
    {
        void Accept(ISpecificator specificator);
        void Execute(UrlInfo urlInfo);
    }
}
