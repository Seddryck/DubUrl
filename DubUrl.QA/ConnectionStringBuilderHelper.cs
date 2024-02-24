using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.QA;

public class ConnectionStringBuilderHelper
{
    public static DbConnectionStringBuilder Retrieve(string invariantName, DbProviderFactory dbProviderFactory)
    {
        DbProviderFactories.RegisterFactory(invariantName, dbProviderFactory);

        if (!DbProviderFactories.GetProviderInvariantNames().Contains(invariantName))
            Assert.Ignore($"No provider found for {invariantName}");

        var factory = DbProviderFactories.GetFactory(invariantName);
        var csb = factory.CreateConnectionStringBuilder();
        if (csb == null)
            Assert.Fail($"Provider found for {invariantName} but can't find a connection string builder");
        else
            return csb;
        throw new InvalidProgramException();
    }
}
