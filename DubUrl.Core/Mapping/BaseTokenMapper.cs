using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public abstract class BaseTokenMapper
    {
        private ISpecificator? _specificator;
        protected ISpecificator Specificator
        {
            get { return _specificator ?? throw new ArgumentNullException(); }
            set { _specificator = value; }
        }

        public BaseTokenMapper() { }

        public void Accept(ISpecificator specificator)
            => Specificator = specificator;

        internal abstract void Execute(UrlInfo urlInfo);
    }
}
