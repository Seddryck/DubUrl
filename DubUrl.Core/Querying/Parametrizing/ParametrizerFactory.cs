using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing
{
    public class ParametrizerFactory
    {
        public static IParametrizer Instantiate(Type ParametrizerType)
            => (IParametrizer)Activator.CreateInstance(ParametrizerType)!;

        public static IParametrizer Instantiate<T>() where T : IParametrizer
            => Instantiate(typeof(T));
    }
}
