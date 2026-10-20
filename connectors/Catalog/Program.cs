using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;

if (args.Length != 3) throw new ArgumentException("Expected matrix path, output path, and version.");
var version = Regex.Match(args[2], @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:[-+].+)?$");
if (!version.Success) throw new ArgumentException("Invalid connector version.");
using var matrix = JsonDocument.Parse(File.ReadAllText(args[0]));
if (matrix.RootElement.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidOperationException("Expected matrix schema version 1.");
var rows = matrix.RootElement.GetProperty("connectors").EnumerateArray()
    .Where(row => row.GetProperty("status").GetString() == "ready").OrderBy(row => row.GetProperty("id").GetString(), StringComparer.Ordinal).ToArray();
foreach (var key in new[] { "id" })
    if (rows.Select(row => row.GetProperty(key).GetString()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Length)
        throw new InvalidOperationException($"Connector {key} values must be unique.");
var assemblies = Directory.GetFiles(AppContext.BaseDirectory, "DubUrl.Provider.*.dll")
    .Append(Path.Combine(AppContext.BaseDirectory, "DubUrl.OleDb.dll")).Select(Assembly.LoadFrom).ToArray();
var mappings = AttributeProviderModule.Discover(assemblies);
var entries = rows.Select(row =>
{
    string Value(string name) => row.GetProperty(name).GetString()!;
    var database = Value("database");
    var mapping = AttributeProviderModule.SelectMapping(mappings, database, Value("technology"));
    return new ConnectorDefinition
    {
        Provider = Value("id"), DatabaseName = database, Version = args[2],
        DubUrlApiRange = $"[{version.Groups["major"].Value}.{version.Groups["minor"].Value}.{version.Groups["patch"].Value},{int.Parse(version.Groups["major"].Value) + 1}.0.0)",
        Assembly = mapping.DialectType.Assembly.GetName().Name + ".dll",
        ImplementationAssemblies = new[] { "DubUrl.Provider." + Value("family") + ".dll" }
            .Concat(Value("technology") == "System.Data.Odbc" ? new[] { "DubUrl.Provider.Odbc.dll" }
                : Value("technology") == "System.Data.OleDb" ? new[] { "DubUrl.OleDb.dll" } : Array.Empty<string>()).ToArray(),
        Driver = Value("technology"),
        DriverAssembly = matrix.RootElement.GetProperty("technologies").EnumerateArray()
            .Single(technology => technology.GetProperty("name").GetString() == Value("technology"))
            .TryGetProperty("assembly", out var driverAssembly) ? driverAssembly.GetString() + ".dll" : null,
        Aliases = row.GetProperty("defaultAliases").EnumerateArray().Select(item => item.GetString()!).ToArray(),
        Schemes = mapping.Aliases, TargetFrameworks = ["net8.0", "net9.0", "net10.0"],
        RuntimeIdentifiers = row.GetProperty("runtimeIdentifiers").EnumerateArray().Select(item => item.GetString()!).ToArray()
    };
}).ToArray();
var json = JsonSerializer.Serialize(new { schemaVersion = 1, entries }, new JsonSerializerOptions
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) + "\n";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
if (!File.Exists(args[1]) || File.ReadAllText(args[1]) != json) File.WriteAllText(args[1], json);
