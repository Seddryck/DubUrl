using DubUrl.Parsing;

namespace DubUrl.Rewriting.Tokening;

/// <summary>Shared URL-to-file mapping; providers supply keywords, labels, and empty-path behavior.</summary>
public class FilePathTokenMapper : BaseTokenMapper
{
    private readonly string keyword;
    private readonly string rootPath;
    private readonly string databaseName;
    private readonly string? emptyValue;

    public FilePathTokenMapper(string keyword, string rootPath, string databaseName, string? emptyValue = null)
    {
        this.keyword = keyword;
        this.rootPath = string.IsNullOrEmpty(rootPath) || rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath : rootPath + Path.DirectorySeparatorChar;
        this.databaseName = databaseName;
        this.emptyValue = emptyValue;
    }

    public override void Execute(ConnectionUri urlInfo)
    {
        if (emptyValue is not null && string.IsNullOrEmpty(urlInfo.Host) && urlInfo.Segments.Count == 0)
        {
            Specificator.Execute(keyword, emptyValue);
            return;
        }
        var segments = GetSegments(urlInfo);
        if (segments.Count == 0)
            throw new InvalidConnectionUrlMissingSegmentsException(databaseName);
        Specificator.Execute(keyword, PathHelper.Create(rootPath, segments));
    }

    protected virtual IReadOnlyList<string> GetSegments(ConnectionUri urlInfo)
    {
        if (string.IsNullOrEmpty(urlInfo.Host) && urlInfo.Segments.Count > 1
            && string.IsNullOrEmpty(urlInfo.Segments[0]))
            return GetLocalSegments(urlInfo.Segments);

        var segments = new List<string>();
        if (!string.Equals(urlInfo.Host, "localhost", StringComparison.InvariantCultureIgnoreCase)
            && !string.Equals(urlInfo.Host, ".", StringComparison.InvariantCultureIgnoreCase))
            segments.Add(urlInfo.Host);
        segments.AddRange(urlInfo.Segments);
        return segments;
    }

    private static IReadOnlyList<string> GetLocalSegments(IReadOnlyList<string> segments)
    {
        // Older callers construct two leading empty segments instead of using Parser.
        // Those retain their historical root-relative behavior on every platform.
        if (string.IsNullOrEmpty(segments[1]))
            return segments.SkipWhile(string.IsNullOrEmpty).ToArray();
        var absoluteSegments = segments.Skip(1).ToArray();
        var first = absoluteSegments[0];
        var windowsDrive = first.Length == 2 && char.IsLetter(first[0]) && first[1] == ':';
        if (!OperatingSystem.IsWindows() && !windowsDrive)
            absoluteSegments[0] = Path.DirectorySeparatorChar + first;
        return absoluteSegments;
    }
}
