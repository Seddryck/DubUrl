using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    public class DriverLocatorFactory : BaseLocatorFactory
    {
        private DriverLocatorIntrospector DriverLocatorIntrospector { get; set; } = new DriverLocatorIntrospector();

        public DriverLocatorFactory()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            foreach (var driverLocator in DriverLocatorIntrospector.Locate())
                AddSchemes(driverLocator.Aliases, driverLocator.DriverLocatorType);

            void AddSchemes(string[] aliases, Type driverLocator)
            {
                foreach (var alias in aliases)
                    AddDriver(alias, driverLocator);
            }
        }

        public virtual IDriverLocator Instantiate(string scheme)
            => Instantiate(scheme, new Dictionary<Type, object>());
        public virtual IDriverLocator Instantiate(string scheme, IDictionary<Type, object> options)
        {
            if (!Schemes.ContainsKey(scheme))
                throw new ArgumentException();

            var driverLocatorType = Schemes[scheme];
            var ctors = driverLocatorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            var ctor = ctors.FirstOrDefault(
                x => x.GetParameters().Length == options.Count
                    && x.GetParameters().All(x => options.ContainsKey(x.ParameterType))
                ) ?? throw new NullReferenceException();
            var parameters = new List<object>(ctor.GetParameters().Length);
            ctor.GetParameters().ToList().ForEach(x => parameters.Add(options[x.ParameterType]));

            return ctor.Invoke(parameters.ToArray()) as IDriverLocator
                ?? throw new NullReferenceException();
        }

        public void AddDriver(string alias, Type locator) => AddElement(alias, locator);
    }
}