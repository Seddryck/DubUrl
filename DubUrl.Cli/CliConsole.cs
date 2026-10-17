namespace DubUrl.Cli;

public interface ICliConsole
{
    TextReader In { get; }
    TextWriter Out { get; }
    TextWriter Error { get; }
    bool IsInputRedirected { get; }
    bool IsInteractive { get; }
}

internal sealed class SystemCliConsole : ICliConsole
{
    public TextReader In => Console.In;
    public TextWriter Out => Console.Out;
    public TextWriter Error => Console.Error;
    public bool IsInputRedirected => Console.IsInputRedirected;
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;
}
