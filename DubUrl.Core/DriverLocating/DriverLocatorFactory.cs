using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.DriverLocating
{
    public class DriverLocatorFactory
    {
        private readonly record struct ProviderInfo(string ProviderName, Type Mapper);
        private readonly Dictionary<string, Type> Schemes = new();

        public DriverLocatorFactory()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            AddSchemes(new[] { "mssql", "ms", "sqlserver" }, typeof(MssqlDriverLocator));
            AddSchemes(new[] { "pgsql", "postgres", "pg", "postgresql" }, typeof(PostgresqlDriverLocator));
            AddSchemes(new[] { "mysql", "my" }, typeof(MySqlConnectorDriverLocator));

            void AddSchemes(string[] aliases, Type driverLocator)
            {
                foreach (var alias in aliases)
                    AddDriverLocator(alias, driverLocator);
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
                x => x.GetParameters().Count() == options.Count
                    && x.GetParameters().All(x => options.ContainsKey(x.ParameterType))
                ) ?? throw new NullReferenceException();
            var parameters = new List<object>(ctor.GetParameters().Count());
            ctor.GetParameters().ToList().ForEach(x => parameters.Add(options[x.ParameterType]));

            return ctor.Invoke(parameters.ToArray()) as IDriverLocator
                ?? throw new NullReferenceException();
        }

        #region Add, remove aliases and mappings

        public void AddAlias(string alias, string original)
        {
            if (Schemes.ContainsKey(alias))
                throw new ArgumentException();

            if (!Schemes.ContainsKey(original))
                throw new ArgumentException();

            Schemes.Add(alias, Schemes[original]);
        }

        public void AddDriverLocator(string alias, Type driverLocator)
        {
            if (Schemes.ContainsKey(alias))
                throw new ArgumentException();

            if (!driverLocator.IsAssignableTo(typeof(IDriverLocator)))
                throw new ArgumentException();

            Schemes.Add(alias, driverLocator);
        }

        public void ReplaceMapping(Type oldDriverLocator, Type newDriverLocator)
        {
            foreach (var scheme in Schemes.Where(x => x.Value == oldDriverLocator))
                Schemes[scheme.Key] = newDriverLocator;
        }

        #endregion


    }
}