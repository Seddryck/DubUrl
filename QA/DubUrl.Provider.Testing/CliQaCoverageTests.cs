using System.Text.RegularExpressions;
using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Provider.Testing;

public class CliQaCoverageTests
{
    [TestCase(typeof(ProviderContract))]
    [TestCase(typeof(OdbcContract))]
    [TestCase(typeof(OleDbContract))]
    [TestCase(typeof(AdomdContract))]
    public void EveryTransportContractIncludesTheSharedCliCases(Type contract)
        => Assert.That(typeof(CliConnectionCheckContract).IsAssignableFrom(contract), Is.True);

    [TestCase(typeof(ProviderContract))]
    [TestCase(typeof(OdbcContract))]
    [TestCase(typeof(OleDbContract))]
    [TestCase(typeof(AdomdContract))]
    public void EveryTransportContractIncludesThePasswordStrategyCases(Type contract)
        => Assert.That(typeof(PasswordAuthenticationQaContract).IsAssignableFrom(contract), Is.True);

    [TestCase(typeof(ProviderContract))]
    [TestCase(typeof(OdbcContract))]
    [TestCase(typeof(OleDbContract))]
    [TestCase(typeof(AdomdContract))]
    public void EveryTransportContractIncludesTheCliPasswordConsumerCases(Type contract)
        => Assert.That(typeof(CliPasswordAuthenticationQaContract).IsAssignableFrom(contract), Is.True);

    [Test]
    public void EveryExistingProviderQaProjectUsesATransportContract()
    {
        var providers = Path.Combine(FindRoot(), "Providers");
        var projects = Directory.EnumerateFiles(providers, "*.QA.csproj", SearchOption.AllDirectories).ToArray();
        Assert.That(projects, Has.Length.EqualTo(22), "Review the CLI coverage inventory when adding a provider QA suite.");
        foreach (var project in projects)
        {
            var source = string.Join('\n', Directory.EnumerateFiles(Path.GetDirectoryName(project)!, "*.cs")
                .Select(File.ReadAllText));
            Assert.That(Regex.IsMatch(source, @":\s*(?:ProviderContract|OdbcContract|OleDbContract|AdomdContract)\b",
                RegexOptions.None, TimeSpan.FromSeconds(1)), Is.True, Path.GetFileName(project));
        }
    }
    [TestCase(typeof(ProviderContract))]
    [TestCase(typeof(OdbcContract))]
    [TestCase(typeof(OleDbContract))]
    [TestCase(typeof(AdomdContract))]
    public void EveryTransportContractIncludesTheNativeTokenCases(Type contract)
        => Assert.That(typeof(TokenAuthenticationQaContract).IsAssignableFrom(contract), Is.True);
    [TestCase(typeof(ProviderContract))]
    [TestCase(typeof(OdbcContract))]
    [TestCase(typeof(OleDbContract))]
    [TestCase(typeof(AdomdContract))]
    public void EveryTransportContractIncludesTheClientCertificateCases(Type contract)
        => Assert.That(typeof(ClientCertificateAuthenticationQaContract).IsAssignableFrom(contract), Is.True);
    [TestCase(typeof(ProviderContract))]
    [TestCase(typeof(OdbcContract))]
    [TestCase(typeof(OleDbContract))]
    [TestCase(typeof(AdomdContract))]
    public void EveryTransportContractIncludesTheOperatingSystemIdentityCases(Type contract)
        => Assert.That(typeof(OperatingSystemIdentityQaContract).IsAssignableFrom(contract), Is.True);
    [TestCase(typeof(OdbcContract), "ODBC")]
    [TestCase(typeof(OleDbContract), "OleDB")]
    public void StrictTransportCategoriesDoNotSelectOptionalAuthenticationCases(Type contract, string category)
    {
        Assert.That(contract.GetCustomAttributes(typeof(CategoryAttribute), inherit: false), Is.Empty);
        var tests = contract.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(typeof(TestAttribute), inherit: false).Length > 0).ToArray();
        Assert.That(tests, Has.Length.EqualTo(3));
        foreach (var test in tests)
            Assert.That(test.GetCustomAttributes(typeof(CategoryAttribute), inherit: false).Cast<CategoryAttribute>().Select(attribute => attribute.Name), Does.Contain(category));
    }
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DubUrl.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run the QA coverage inventory from the repository checkout.");
    }
}
