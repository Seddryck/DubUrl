using System.Text.Json;
using DubUrl.Application;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

internal sealed class CliPasswordQaSchema
{
    internal string Table { get; } = "qa_cli_auth_" + Guid.NewGuid().ToString("N")[..16];
    internal string Document => JsonSerializer.Serialize(new
    {
        version = 1,
        tables = new[] { new { identity = new { name = Table }, columns = new[] { new { name = "id", type = "int32" } } } }
    });
    internal static void RequireDeployment(PasswordQaDriver driver)
    {
        driver.RequirePasswordAuthentication();
        if (driver.Family is not ("sqlserver" or "postgresql" or "timescale" or "mysql" or "mariadb"))
            Assert.Ignore($"#1690: {driver.Family} QA does not provision a verified CLI schema-deployment recipe/admin principal. Firebird is SELECT-only; QuestDB has no matching schema renderer. No deployment pass is inferred from connection success.");
    }
    internal static void RequireDryRun(PasswordQaDriver driver)
    {
        if (driver.Family is not ("sqlserver" or "postgresql" or "timescale" or "mysql" or "mariadb" or "sqlite" or "duckdb"))
            Assert.Ignore($"#1690: {driver.Family} QA does not have a verified schema-rendering recipe for the authentication dry-run case.");
    }
    internal async Task VerifyAsync(PasswordQaDriver driver)
    {
        using var source = new PasswordQaSource(driver.Credentials);
        await using var connection = await driver.Target(source).OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = ConnectionCheckSql.Table(driver.Family, Table);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.That(await reader.ReadAsync(), Is.False, "The CLI must have created the empty fixture-owned table.");
    }
    internal async Task DropAsync(PasswordQaDriver driver)
    {
        using var source = new PasswordQaSource(driver.Credentials);
        await using var connection = await driver.Target(source).OpenAsync();
        await using var command = connection.CreateCommand();
        var select = ConnectionCheckSql.Table(driver.Family, Table);
        var identifier = select["SELECT 1 FROM ".Length..^" WHERE 1=0".Length];
        command.CommandText = "DROP TABLE IF EXISTS " + identifier;
        await command.ExecuteNonQueryAsync();
    }
}
