using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DubUrl.Cli;

/// <summary>CLI values only; all provider policy and mapping belongs to Core/provider modules.</summary>
internal sealed class CliConnectionOptions
{
    internal static IReadOnlyList<string> Names { get; } = ["--tls", "--ca-certificate", "--connection-timeout", "--command-timeout", "--application-name", "--time-zone", "--pooling", "--pool-min-size", "--pool-max-size", "--pool-idle-timeout", "--pool-max-lifetime"];
    private readonly Option<bool> tls = new("--tls") { Description = "Require TLS, certificate-chain validation and hostname verification." };
    private readonly Option<string?> ca = new("--ca-certificate") { Description = "PEM CA file for server trust. Implies --tls." };
    private readonly Option<string?> connectionTimeout = new("--connection-timeout") { Description = "Native opening timeout: e.g. 15s, 500ms, 2m, or infinite." };
    private readonly Option<string?> commandTimeout = new("--command-timeout") { Description = "Native default command timeout: e.g. 30s or infinite. Separate from opening." };
    private readonly Option<string?> application = new("--application-name") { Description = "Application identification in native connection metadata." };
    private readonly Option<string?> zone = new("--time-zone") { Description = "Database session time zone, e.g. Europe/Brussels or +02:00." };
    private readonly Option<string?> pooling = new("--pooling") { Description = "Enable or disable native pooling: enabled or disabled." };
    private readonly Option<int?> min = new("--pool-min-size") { Description = "Minimum native pool size." };
    private readonly Option<int?> max = new("--pool-max-size") { Description = "Maximum native pool size." };
    private readonly Option<string?> idle = new("--pool-idle-timeout") { Description = "Native pool idle timeout, with duration units." };
    private readonly Option<string?> lifetime = new("--pool-max-lifetime") { Description = "Maximum physical connection age, with duration units." };
    private Option[] Options => [tls, ca, connectionTimeout, commandTimeout, application, zone, pooling, min, max, idle, lifetime];
    internal void AddTo(Command command) { foreach (var option in Options) command.Options.Add(option); }
    internal string? CaPath(ParseResult result) => result.GetValue(ca);

    internal ConnectionUrl Configure(ParseResult result, ConnectionUrl target)
    {
        if (Options.Any(o => result.GetResult(o)?.IdentifierTokenCount > 1))
            throw new CliOAuthInputException("Specify each connection option only once.");
        if (result.GetValue(tls) || result.GetValue(ca) is not null)
            target = target.WithEncryption(ConnectionEncryption.Tls(result.GetValue(ca)));
        target = ConfigureTimeouts(result, target);
        target = ConfigureSession(result, target);
        return ConfigurePooling(result, target);
    }

    private ConnectionUrl ConfigureTimeouts(ParseResult result, ConnectionUrl target)
    {
        if (result.GetValue(connectionTimeout) is string connect) target = target.WithConnectionTimeout(ParseDuration(connect));
        if (result.GetValue(commandTimeout) is string command) target = target.WithCommandTimeout(ParseDuration(command));
        return target;
    }

    private ConnectionUrl ConfigureSession(ParseResult result, ConnectionUrl target)
    {
        if (result.GetValue(application) is string name) target = target.WithApplicationName(name);
        if (result.GetValue(zone) is string timeZone) target = target.WithTimeZone(timeZone);
        return target;
    }

    private ConnectionUrl ConfigurePooling(ParseResult result, ConnectionUrl target)
    {
        if (!new Option[] { pooling, min, max, idle, lifetime }.Any(o => result.GetResult(o) is { Implicit: false })) return target;
        var enabled = result.GetValue(pooling) switch
        {
            null or "enabled" => true,
            "disabled" => false,
            _ => throw new CliOAuthInputException("--pooling must be enabled or disabled.")
        };
        return target.WithPooling(new()
        {
            Enabled = enabled, MinSize = result.GetValue(min), MaxSize = result.GetValue(max),
            IdleTimeout = result.GetValue(idle) is string idleValue ? ParseDuration(idleValue) : null,
            MaxLifetime = result.GetValue(lifetime) is string age ? ParseDuration(age) : null
        });
    }

    internal static TimeSpan ParseDuration(string value)
    {
        if (value == "infinite") return TimeSpan.Zero;
        var match = Regex.Match(value, @"^(\d+(?:\.\d+)?)(ms|s|m|h)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            throw new CliOAuthInputException("Use a nonnegative duration with ms, s, m or h units, or infinite.");
        var seconds = number * (match.Groups[2].Value switch { "ms" => .001, "m" => 60, "h" => 3600, _ => 1 });
        if (!double.IsFinite(seconds) || seconds > int.MaxValue || (number > 0 && seconds < .0000001))
            throw new CliOAuthInputException("Duration must fit 0 through 2147483647 seconds; positive values round up to seconds.");
        return TimeSpan.FromSeconds(seconds);
    }
}
