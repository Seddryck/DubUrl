using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    public abstract class BaseTokenMapper : ITokenMapper
    {
        private ISpecificator? specificator;
        protected ISpecificator Specificator
        {
            get { return specificator ?? throw new ArgumentNullException(); }
            set { specificator = value; }
        }

        public BaseTokenMapper() { }

        public void Accept(ISpecificator specificator)
            => Specificator = specificator;

        public abstract void Execute(UrlInfo urlInfo);
    }
}
