using DubUrl.ProviderTesting;
using NUnit.Framework;
namespace DubUrl.Provider.MariaDb.QA;
public sealed class QaManifestTests
{
    [Test, Category("Manifest")]
    public void ManifestIsValid() => QaManifest.Validate(Path.Combine(AppContext.BaseDirectory, "qa-manifest.json"), "mariadb", "Connection", "Querying", "ODBC");
    [Test, Category("MariaDB")]
    public void ServerIsMariaDb()
    {
        new DubUrl.Registering.ProviderFactoriesRegistrator().Register();
        using var connection = new ConnectionUrl(MariaDbTestDatabase.ConnectionUrl).Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select version()";
        Assert.That(Convert.ToString(command.ExecuteScalar()), Does.Contain("MariaDB"));
    }
}
