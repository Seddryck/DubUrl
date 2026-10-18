using DubUrl.Parsing;

namespace DubUrl.Rewriting.Tokening;

public class HostTokenMapper(string keyword, string defaultValue = "") : BaseTokenMapper
{
    public override void Execute(ConnectionUri urlInfo)
        => Specificator.Execute(keyword, string.IsNullOrEmpty(urlInfo.Host) ? defaultValue : urlInfo.Host);
}

public class PortTokenMapper(string keyword, int defaultValue = 0) : BaseTokenMapper
{
    public override void Execute(ConnectionUri urlInfo)
    {
        var port = urlInfo.Port == 0 ? defaultValue : urlInfo.Port;
        if (port != 0)
            Specificator.Execute(keyword, port);
    }
}

public class SegmentTokenMapper(string keyword, int index) : BaseTokenMapper
{
    public override void Execute(ConnectionUri urlInfo)
    {
        if (urlInfo.Segments.Count > index)
            Specificator.Execute(keyword, urlInfo.Segments[index]);
    }
}

public class SingleSegmentTokenMapper(string keyword, string databaseName, bool required) : BaseTokenMapper
{
    public override void Execute(ConnectionUri urlInfo)
    {
        if (urlInfo.Segments.Count > 1)
            throw new InvalidConnectionUrlTooManySegmentsException(databaseName, urlInfo.Segments);
        if (urlInfo.Segments.Count == 1)
            Specificator.Execute(keyword, urlInfo.Segments[0]);
        else if (required)
            throw new InvalidConnectionUrlMissingSegmentsException(databaseName);
    }
}
