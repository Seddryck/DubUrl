namespace DubUrl;

/// <summary>Provider-native pooling, not DubUrl's connection retention policy.</summary>
public sealed record ConnectionPoolingOptions
{
    public bool Enabled { get; init; } = true;
    public int? MinSize { get; init; }
    public int? MaxSize { get; init; }

    internal void Validate()
    {
        if (MinSize < 0) throw new ArgumentOutOfRangeException(nameof(MinSize));
        if (MaxSize <= 0) throw new ArgumentOutOfRangeException(nameof(MaxSize));
        if (MinSize > MaxSize) throw new ArgumentException("Minimum pool size cannot exceed maximum pool size.");
        if (!Enabled && (MinSize.HasValue || MaxSize.HasValue))
            throw new ArgumentException("Pool sizes cannot be specified when pooling is disabled.");
    }
}
