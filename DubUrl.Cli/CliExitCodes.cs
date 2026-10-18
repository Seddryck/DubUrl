namespace DubUrl.Cli;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int UnexpectedError = 1;
    public const int InvalidInput = 2;
    public const int ValidationFailed = 3;
    public const int DeploymentFailed = 4;
    public const int ProviderOperationFailed = 5;
    public const int ProviderNotFound = 6;
    public const int Cancelled = 130;
}
