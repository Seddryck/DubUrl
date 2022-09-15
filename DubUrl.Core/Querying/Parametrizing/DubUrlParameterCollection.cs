using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing
{
    public class DubUrlParameterCollection
    {
        private DubUrlParameterFactory DubUrlParameterFactory { get; }
        private readonly List<DubUrlParameter> parameters = new ();

        public DubUrlParameterCollection()
            => DubUrlParameterFactory = new DubUrlParameterFactory();

        public DubUrlParameterCollection Add<T>(string name, T? value)
        {
            var param = DubUrlParameterFactory.Instantiate(name, value);
            parameters.Add(param);
            return this;
        }

        public DubUrlParameter[] ToArray()
            => parameters.ToArray();
    }
}
