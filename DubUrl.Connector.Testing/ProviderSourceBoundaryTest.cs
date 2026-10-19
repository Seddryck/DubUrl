using System.Xml.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DubUrl.Connector.Testing;

public class ProviderSourceBoundaryTest
{
    [Test]
    public void ProviderSources_UseTheirProjectAndFolderNamespace()
    {
        var providers = Path.Combine(FindRepositoryRoot(), "providers");
        var violations = new List<string>();
        foreach (var project in Directory.EnumerateDirectories(providers, "DubUrl.Provider.*"))
        {
            foreach (var file in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")))
            {
                var folder = Path.GetDirectoryName(Path.GetRelativePath(project, file))!;
                var expected = Path.GetFileName(project) + "." + folder.Replace(Path.DirectorySeparatorChar, '.');
                var actual = Regex.Match(File.ReadAllText(file), @"(?m)^namespace ([\w.]+);").Groups[1].Value;
                if (actual != expected)
                    violations.Add($"{Path.GetRelativePath(providers, file)}: expected {expected}, found {actual}");
            }
        }

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ProviderProjects_DoNotLinkImplementationSourcesOrResourcesFromCore()
    {
        var root = FindRepositoryRoot();
        var providers = Path.Combine(root, "providers");
        var files = Directory.EnumerateFiles(providers, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".csproj" or ".props" or ".targets");
        var violations = files.SelectMany(path => XDocument.Load(path).Descendants()
            .Where(item => item.Name.LocalName is "Compile" or "EmbeddedResource")
            .SelectMany(item => new[] { "Include", "Update" }
                .Select(attribute => item.Attribute(attribute)?.Value)
                .OfType<string>())
            .Where(include => include.Replace('\\', '/').Contains("DubUrl.Core/", StringComparison.OrdinalIgnoreCase))
            .Select(include => $"{Path.GetRelativePath(root, path)}: {include}"));

        Assert.That(violations, Is.Empty,
            "Provider implementation sources and resources belong in provider projects; use a project reference for shared Core contracts.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DubUrl.sln")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Run source-boundary tests from the repository checkout.");
    }
}
