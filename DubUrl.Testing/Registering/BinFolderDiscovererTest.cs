using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Registering;
using NUnit.Framework;

namespace DubUrl.Testing.Registering
{
    public class BinFolderDiscovererTest
    {
        [Test]
        public void Execute_CurrentAssembly_ExpectedMembers()
        {
            var discoverer = new BinFolderDiscoverer();
            var types = discoverer.Execute();
            Assert.That(types, Is.Not.Null);
            Assert.That(types, Is.Not.Empty);
            Assert.That(types.Select(x => x.Name), Does.Contain("NpgsqlFactory"));
            Assert.That(types.Select(x => x.Name), Does.Contain("SqlClientFactory"));
            Assert.That(types.Select(x => x.FullName), Does.Contain("Npgsql.NpgsqlFactory"));
            Assert.That(types.Select(x => x.FullName), Does.Contain("Microsoft.Data.SqlClient.SqlClientFactory"));
        }
    }
}
