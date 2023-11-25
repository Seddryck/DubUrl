using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.StringTemplate.Debug;
using DubUrl.Rewriting.Tokening;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing.Rewriting.Tokening
{
    public class SpecificatorTest
    {
        [Test]
        public void Execute_ValidKey_ValueAdded()
        {
            var connectionStringBuilder = new Mock<DbConnectionStringBuilder>();
            connectionStringBuilder.Setup(x => x.ContainsKey(It.IsAny<string>())).Returns(true);

            var specificator = new Specificator(connectionStringBuilder.Object);
            specificator.Execute("key", "value");
            connectionStringBuilder.Verify(x => x.ContainsKey("key"));
            connectionStringBuilder.VerifySet(x => x["key"] = "value");
            connectionStringBuilder.VerifyNoOtherCalls();
        }

        [Test]
        public void Execute_InvalidKey_ValueNotAdded()
        {
            var connectionStringBuilder = new Mock<DbConnectionStringBuilder>();
            connectionStringBuilder.Setup(x => x.ContainsKey(It.IsAny<string>())).Returns(false);

            var specificator = new Specificator(connectionStringBuilder.Object);
            Assert.Throws<InvalidOperationException>(() => specificator.Execute("key", "value"));
            connectionStringBuilder.Verify(x => x.ContainsKey("key"));
            connectionStringBuilder.VerifySet(x => x["key"] = "value", Times.Never);
            connectionStringBuilder.VerifyNoOtherCalls();
        }

        [Test]
        public void Execute_MultipleAssignments_ValueOverridden()
        {
            var connectionStringBuilder = new Mock<DbConnectionStringBuilder>();
            connectionStringBuilder.Setup(x => x.ContainsKey(It.IsAny<string>())).Returns(true);

            var specificator = new Specificator(connectionStringBuilder.Object);
            specificator.Execute("key", "value");
            specificator.Execute("key", "new-value");
            connectionStringBuilder.Verify(x => x.ContainsKey("key"), Times.Exactly(2));
            connectionStringBuilder.VerifySet(x => x["key"] = "value", Times.Once);
            connectionStringBuilder.VerifySet(x => x["key"] = "new-value", Times.Once);
            connectionStringBuilder.VerifyNoOtherCalls();
        }
    }
}
