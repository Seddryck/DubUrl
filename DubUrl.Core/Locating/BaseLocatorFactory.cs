using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating
{
    public abstract class BaseLocatorFactory
    {
        protected readonly Dictionary<string, Type> Schemes = new();

        #region Add, remove aliases and mappings

        public void AddAlias(string alias, string original)
        {
            if (Schemes.ContainsKey(alias))
                throw new ArgumentException();

            if (!Schemes.ContainsKey(original))
                throw new ArgumentException();

            Schemes.Add(alias, Schemes[original]);
        }

        protected void AddElement(string alias, Type locator)
        {
            if (Schemes.ContainsKey(alias))
                throw new ArgumentException();

            Schemes.Add(alias, locator);
        }

        public void ReplaceMapping(Type oldDriverLocator, Type newDriverLocator)
        {
            foreach (var scheme in Schemes.Where(x => x.Value == oldDriverLocator))
                Schemes[scheme.Key] = newDriverLocator;
        }

        #endregion
    }
}
