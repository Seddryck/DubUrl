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

namespace DubUrl.Testing.Rewriting.Tokening;

public class StraightSpecificatorTest
{
    [Test]
    public void Execute_ValidKey_ValueAdded()
    {
        var connectionStringBuilder = new Mock<DbConnectionStringBuilder>();

        var specificator = new StraightSpecificator(connectionStringBuilder.Object);
        specificator.Execute("key", "value");
        connectionStringBuilder.Verify(x => x.ContainsKey("key"), Times.Never);
        connectionStringBuilder.VerifySet(x => x["key"] = "value");
        connectionStringBuilder.VerifyNoOtherCalls();
    }

    [Test]
    public void Execute_MultipleAssignments_ValueOverridden()
    {
        var connectionStringBuilder = new Mock<DbConnectionStringBuilder>();

        var specificator = new StraightSpecificator(connectionStringBuilder.Object);
        specificator.Execute("key", "value");
        specificator.Execute("key", "new-value");
        connectionStringBuilder.Verify(x => x.ContainsKey("key"), Times.Never);
        connectionStringBuilder.VerifySet(x => x["key"] = "value", Times.Once);
        connectionStringBuilder.VerifySet(x => x["key"] = "new-value", Times.Once);
        connectionStringBuilder.VerifyNoOtherCalls();
    }
}
