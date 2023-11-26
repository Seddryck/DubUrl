using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Parametrizing;
using NUnit.Framework;

namespace DubUrl.Testing.Querying.Parametrizing
{
    public class DubUrlParameterCollectionTest
    {
        [Test]
        public void Add_Parameter_NotEmpty()
        {
            var collection = new DubUrlParameterCollection();
            collection.Add("param101", 101);
            Assert.That(collection.ToArray(), Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(collection.ToArray()[0].Name, Is.EqualTo("param101"));
                Assert.That(collection.ToArray()[0].Value, Is.EqualTo(101));
            });
        }
    }
}
