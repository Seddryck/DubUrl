using DubUrl.Locating.OleDbProvider.Implementation;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider
{
    public class ProviderLocatorFactory : BaseLocatorFactory
    {
        public ProviderLocatorFactory()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            AddSchemes(new[] { "mssql", "ms", "sqlserver" }, typeof(MssqlOleDbProviderLocator) );
            AddSchemes(new[] { "mssqlncli" }, typeof(MssqlNCliProviderLocator));
            AddSchemes(new[] { "mysql", "my" }, typeof(MySqlProviderLocator));
            AddSchemes(new[] { "xls" }, typeof(AceXlsProviderLocator));
            AddSchemes(new[] { "xlsx" }, typeof(AceXlsxProviderLocator));
            AddSchemes(new[] { "xlsm" }, typeof(AceXlsmProviderLocator));
            AddSchemes(new[] { "xlsb" }, typeof(AceXlsbProviderLocator));

            void AddSchemes(string[] aliases, Type locator)
            {
                foreach (var alias in aliases)
                    AddProvider(alias, locator);
            }
        }

        public virtual IProviderLocator Instantiate(string scheme)
            => Instantiate(scheme, new Dictionary<Type, object>());
        public virtual IProviderLocator Instantiate(string scheme, IDictionary<Type, object> options)
        {
            if (!Schemes.ContainsKey(scheme))
                throw new ArgumentException();

            var providerLocatorType = Schemes[scheme];
            var ctors = providerLocatorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            var ctor = ctors.FirstOrDefault(
                x => x.GetParameters().Length == options.Count
                    && x.GetParameters().All(x => options.ContainsKey(x.ParameterType))
                ) ?? throw new NullReferenceException();
            var parameters = new List<object>(ctor.GetParameters().Length);
            ctor.GetParameters().ToList().ForEach(x => parameters.Add(options[x.ParameterType]));

            return ctor.Invoke(parameters.ToArray()) as IProviderLocator
                ?? throw new NullReferenceException();
        }

        public void AddProvider(string alias, Type locator) => AddElement(alias, locator);

    }
}