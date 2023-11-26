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
    public class UniqueAssignmentSpecificatorTest
    {
        [Test]
        public void Execute_ValidKey_ValueAdded()
        {
            var connectionStringBuilder = new Mock<DbConnectionStringBuilder>();
            connectionStringBuilder.Setup(x => x.ContainsKey(It.IsAny<string>())).Returns(false);

            var specificator = new UniqueAssignmentSpecificator(connectionStringBuilder.Object);
            specificator.Execute("key", "value");
            connectionStringBuilder.Verify(x => x.ContainsKey("key"), Times.Once);
            connectionStringBuilder.VerifySet(x => x["key"] = "value");
            connectionStringBuilder.VerifyNoOtherCalls();
        }

        [Test]
        public void Execute_MultipleAssignments_ValueNotOverridden()
        {
            var connectionStringBuilder = new Mock<DbConnectionStringBuilder>();
            var sequence = new MockSequence();
            connectionStringBuilder.InSequence(sequence).Setup(x => x.ContainsKey(It.IsAny<string>())).Returns(false);
            connectionStringBuilder.InSequence(sequence).Setup(x => x.ContainsKey(It.IsAny<string>())).Returns(true);

            var specificator = new UniqueAssignmentSpecificator(connectionStringBuilder.Object);
            specificator.Execute("key", "value");
            Assert.Throws<InvalidOperationException>(() => specificator.Execute("key", "new-value"));
            connectionStringBuilder.Verify(x => x.ContainsKey("key"), Times.Exactly(2));
            connectionStringBuilder.VerifySet(x => x["key"] = "value", Times.Once);
            connectionStringBuilder.VerifySet(x => x["key"] = "new-value", Times.Never);
            connectionStringBuilder.VerifyNoOtherCalls();
        }
    }
}
