namespace DubUrl.Cli;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int CredentialAbsent = 1;
    public const int CredentialStoreFailed = 10;
    public const int ConnectionCheckFailed = 5;
    public const int UnexpectedError = 1;
    public const int InvalidInput = 2;
    public const int ValidationFailed = 3;
    public const int DeploymentFailed = 4;
    public const int ProviderOperationFailed = 5;
    public const int ProviderNotFound = 6;
    public const int InspectionIncomplete = 7;
    public const int InspectionUnsupported = 8;
    public const int InspectionFailed = 9;
    public const int Cancelled = 130;
}
