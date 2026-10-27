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

    [Test]
    public void EveryExistingProviderQaProjectUsesATransportContract()
    {
        var providers = Path.Combine(FindRoot(), "Providers");
        var projects = Directory.EnumerateFiles(providers, "*.QA.csproj", SearchOption.AllDirectories).ToArray();
        Assert.That(projects, Has.Length.EqualTo(19), "Review the CLI coverage inventory when adding a provider QA suite.");
        foreach (var project in projects)
        {
            var source = string.Join('\n', Directory.EnumerateFiles(Path.GetDirectoryName(project)!, "*.cs")
                .Select(File.ReadAllText));
            Assert.That(Regex.IsMatch(source, @":\s*(?:ProviderContract|OdbcContract|OleDbContract|AdomdContract)\b",
                RegexOptions.None, TimeSpan.FromSeconds(1)), Is.True, Path.GetFileName(project));
        }
    }
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DubUrl.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run the QA coverage inventory from the repository checkout.");
    }
}
