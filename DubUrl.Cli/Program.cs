using DubUrl.Cli;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    await using var services = CliComposition.CreateDefault();
    return await CliHost.InvokeAsync(args, services, cancellation.Token);
}
catch (DubUrl.Application.ConnectorOperationException exception)
{
    Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
    return CliExitCodes.ProviderOperationFailed;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
{
    Console.Error.WriteLine("connector-catalog-invalid: The connector configuration could not be read or is invalid.");
    return CliExitCodes.ProviderOperationFailed;
}
