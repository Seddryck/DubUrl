using DubUrl.Querying;
using DubUrl.Querying.Reading;
using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA
{
    internal class RepositoryFactory
    {
        private DatabaseUrlFactory DatabaseUrlFactory { get; }

        public RepositoryFactory(DatabaseUrlFactory databaseUrlFactory)
            => DatabaseUrlFactory = databaseUrlFactory;

        public T Instantiate<T>(string url)
            => (T)(
                    Activator.CreateInstance(typeof(T), new object[] { DatabaseUrlFactory, url})
                    ?? throw new InvalidCastException()
                );
    }
}
