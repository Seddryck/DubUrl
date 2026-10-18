param([switch]$GenerateOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$project = Join-Path $PSScriptRoot 'DocumentationExamples'
$utf8 = [Text.UTF8Encoding]::new($false)
$usings = [Collections.Generic.HashSet[string]]::new()
foreach ($ns in @('System','System.IO','System.Linq','System.Collections.Generic','System.Threading','System.Threading.Tasks','System.Data','System.Data.Common','System.Globalization','DubUrl','DubUrl.Schema','DubUrl.Schema.Builders','DubUrl.Schema.Constraints','DubUrl.Schema.Documents','DubUrl.Application','DubUrl.Querying.Dialects','Microsoft.Data.Sqlite')) { [void]$usings.Add("using $ns;") }
$classes = [Collections.Generic.List[string]]::new()
$calls = [Collections.Generic.List[string]]::new()
$compiled = 0
function Snippet($page, $index) {
  $path = Join-Path $repo "docs/_docs/$page.md"
  $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
  $blocks = [regex]::Matches($text, '(?ms)^```csharp\n(.*?)^```')
  if ($index -ge $blocks.Count) { throw "Missing C# block $index in $page" }
  return $blocks[$index].Groups[1].Value.TrimEnd()
}
function Add-Case($name, $code, $prefix = '', $suffix = '', [switch]$CompileOnly) {
  $script:compiled++
  $code = [regex]::Replace($code, '(?m)^using ([\w.]+);\n?', {
    param($match) [void]$usings.Add($match.Value.Trim()); return ''
  })
  $declarations = ''
  $type = [regex]::Match($code, '(?m)^public sealed class ')
  if ($type.Success) { $declarations = $code.Substring($type.Index); $code = $code.Substring(0, $type.Index) }
  $classes.Add("public static class Example_$name { public static async Task Run() { $prefix`n$code`n$suffix }`n$declarations`n}")
  if (-not $CompileOnly) { $calls.Add(('await Example_{0}.Run(); Console.WriteLine("PASS {0}");' -f $name)) }
}
Add-Case ConnectionUrl ((Snippet 'class-connectionUrl' 0) + "`n" + (Snippet 'class-connectionUrl' 1)) '' 'Check.True(opened.State == ConnectionState.Open, "Open returns an open connection"); Check.True(closed.State == ConnectionState.Closed, "Connect returns a closed connection");'
Add-Case Dapper (Snippet 'dapper' 0) '' 'Check.True(customers.Count == 1 && customers[0].CustomerId == 1, "Dapper mapping and bound parameter");'
Add-Case DbReader (Snippet 'dbreader' 0) '' 'Check.True(customers.Count == 2 && customers[0].FullName.Length > 0, "DbReader mapping");'
Add-Case FirstConnection (Snippet 'first-connection' 0) -CompileOnly
Add-Case FirstQueryAsync (Snippet 'first-query' 0) 'var url = Fixture.Url;' 'Check.True(Convert.ToInt64(count) == 2, "Async scalar count");'
Add-Case FirstQuerySync (Snippet 'first-query' 1) 'var url = Fixture.Url;' 'Check.True(Convert.ToInt64(count) == 2, "Sync scalar count");'
Add-Case Scalars ((Snippet 'read-scalars' 0) + "`n" + (Snippet 'read-scalars' 1)) 'using var database = new DatabaseUrl(Fixture.Url);' 'Check.True(Convert.ToInt64(count) == 2 && oldest is null, "Scalar value and SQL NULL");'
foreach ($index in 0..4) { Add-Case "Rows$index" (Snippet 'read-rows' $index) 'using var database = new DatabaseUrl(Fixture.Url);' }
Add-Case Templates (Snippet 'sql-templates' 0) 'using var database = new DatabaseUrl(Fixture.Url);' 'Check.True(Convert.ToInt64(first) == 1 && Convert.ToInt64(second) == 2, "Rendered template values");'
foreach ($index in 0..4) {
  $prefix = 'var url = Fixture.Url;'
  if ($index -ge 3) { $prefix += ' await using var source = new ConnectionSourceUrl(url).Create(new ConnectionSourceOptions { ReuseMode = ConnectionReuseMode.RetainedExclusive });' }
  Add-Case "Sources$index" (Snippet 'connection-source' $index) $prefix
}
Add-Case ValidateSchema (Snippet 'validate-schema' 0) '' 'Check.True(!diagnostics.Any(d => d.Severity == SchemaDocumentDiagnosticSeverity.Error), "Valid document");'
Add-Case GenerateSql (Snippet 'generate-sql' 0) '' 'Check.True(sql.Contains("CREATE TABLE"), "Document SQL rendering");'
Add-Case ReadStream (Snippet 'generate-sql' 1) 'var reader = new SchemaDocumentReader(); using var stream = File.OpenRead("schema.yaml");' 'Check.True(document.Version == 1, "Stream format");'
Add-Case SchemaBuilders ((Snippet 'schema-model' 0) + "`n" + (Snippet 'generate-sql' 2)) '' 'Check.True(sql.Contains("CREATE TABLE") && sql.Contains("CREATE VIEW"), "Builder tables and views");'
Add-Case CustomRule ((Snippet 'customize-schema' 0) + "`n" + (Snippet 'customize-schema' 1)) '' 'var document = new SchemaDocumentReader().Read("schema.yaml"); Check.True(validator.Validate(document).Any(d => d.Code == "APP001"), "Application rule executes");'
# The type is declared after the setup statements for a valid standalone C# snippet.
Add-Case LiteralConverter ((Snippet 'customize-schema' 3) + "`n" + (Snippet 'customize-schema' 2)) '' 'Check.True(new HexInt32Converter().TryConvert("2A").Value is 42, "Hex literal conversion");'
Add-Case ApplicationApi (Snippet 'schema-application-api' 0) 'var document = new SchemaDocumentReader().Read("schema.yaml");' 'Check.True(validation.IsSuccess && rendering.IsSuccess, "Transport-neutral schema API");'
Add-Case DeploymentApi (Snippet 'schema-application-api' 1) 'var document = new SchemaDocumentReader().Read("schema.yaml"); var connectionUrl = new ConnectionUrl(Fixture.Url); var cancellationToken = CancellationToken.None;' -CompileOnly
Add-Case Adomd (Snippet 'adomd-providers' 0) -CompileOnly
Add-Case OleDb (Snippet 'oledb-provider-locators' 0) -CompileOnly
Add-Case Odbc ((Snippet 'odbc-driver-locators' 0) + "`n" + (Snippet 'odbc-driver-locators' 1)) -CompileOnly

$schemaText = [IO.File]::ReadAllText((Join-Path $repo 'docs/_docs/first-schema.md')).Replace("`r`n", "`n")
$yaml = [regex]::Match($schemaText, '(?ms)^```yaml\n(.*?)^```').Groups[1].Value.Trim()
if (-not $yaml) { throw 'First-schema YAML example missing' }
$yamlLiteral = $yaml.Replace('"','""')
$fixture = @"
public static class Fixture {
 public static readonly string Root = Path.Combine(Path.GetTempPath(), "duburl-docs-" + Guid.NewGuid().ToString("N"));
 public static string Url => "sqlite:///" + Path.Combine(Root, "customers.db").Replace('\\', '/');
 public static readonly string InitialDirectory = Directory.GetCurrentDirectory();
 public static void Start() {
  Directory.CreateDirectory(Root); Directory.SetCurrentDirectory(Root);
  File.WriteAllText("schema.yaml", @"$yamlLiteral");
  DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);
  Environment.SetEnvironmentVariable("DUBURL_DATABASE_URL", Url);
  using var connection = new SqliteConnection("Data Source=" + Path.Combine(Root,"customers.db")); connection.Open();
  using var command = connection.CreateCommand();
  command.CommandText = "CREATE TABLE Customer (CustomerId INTEGER, FullName TEXT, BirthDate TEXT); INSERT INTO Customer VALUES (1, 'Ada', NULL), (2, 'Grace', NULL)";
  command.ExecuteNonQuery();
 }
 public static void Stop() {
  Directory.SetCurrentDirectory(InitialDirectory);
  SqliteConnection.ClearAllPools();
  var parent = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Root)!);
  if (!string.Equals(parent, Path.TrimEndingDirectorySeparator(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
      || !Path.GetFileName(Root).StartsWith("duburl-docs-", StringComparison.Ordinal))
   throw new InvalidOperationException("Unexpected example fixture path.");
  if (Directory.Exists(Root)) Directory.Delete(Root, true);
 }
}
public static class Check { public static void True(bool condition, string message) { if (!condition) throw new Exception(message); } }
"@
$body = ($usings | Sort-Object) -join "`n"
$body += "`nFixture.Start();`ntry {`n" + ($calls -join "`n") + "`n} finally { Fixture.Stop(); }`n"
$body += "`nConsole.WriteLine(`"$compiled snippet groups compiled; $($calls.Count) executed.`");`n" + $fixture + "`n" + ($classes -join "`n")
New-Item -ItemType Directory -Force (Join-Path $project 'obj') | Out-Null
[IO.File]::WriteAllText((Join-Path $project 'obj/DocumentationSnippets.cs'), $body, $utf8)
if ($GenerateOnly) { return }
dotnet run --project (Join-Path $project 'DocumentationExamples.csproj') --framework net10.0 -p:TargetFrameworks=net10.0
if ($LASTEXITCODE -ne 0) { throw "Documentation examples failed with exit code $LASTEXITCODE" }
