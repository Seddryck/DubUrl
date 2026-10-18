using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Worker;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        if (args is ["--version"])
        {
            Console.Out.WriteLine(GetVersion());
            return 0;
        }

        if (args.Length > 0)
        {
            Console.Error.WriteLine("Usage: duburl-worker [--version]");
            return 2;
        }

        try
        {
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            await using var services = WorkerComposition.CreateDefault();
            var registry = services.GetRequiredService<WorkerMethodRegistry>();
            await WorkerHost.RunAsync(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                registry,
                GetVersion(),
                cancellation.Token);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"DubUrl.Worker terminated unexpectedly: {exception.Message}");
            return 1;
        }
    }

    private static string GetVersion()
        => typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
}
