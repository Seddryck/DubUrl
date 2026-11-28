using System.Globalization;

namespace DubUrl.Querying;

internal static class SqlServerScript
{
    internal readonly record struct Batch(string Text, int Repetitions);

    internal static List<Batch> Split(string script, CancellationToken cancellationToken)
    {
        var batches = new List<Batch>();
        var scanner = new LineScanner();
        var batchStart = 0;
        var hasSql = false;
        var lineNumber = 0;
        for (var lineStart = 0; lineStart < script.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var remaining = script.AsSpan(lineStart);
            var lineLength = remaining.IndexOfAny('\r', '\n');
            if (lineLength < 0)
                lineLength = remaining.Length;
            var line = remaining[..lineLength];
            scanner.Scan(line);

            if (scanner.GoStart >= 0)
            {
                var repetitions = 1;
                if (scanner.TokenCount > 2 || (scanner.TokenCount == 2
                    && (!int.TryParse(line[scanner.LastTokenStart..scanner.LastTokenEnd], NumberStyles.None,
                        CultureInfo.InvariantCulture, out repetitions) || repetitions < 1)))
                    throw new FormatException($"Invalid GO separator on line {lineNumber}. Use GO or GO followed by a positive integer.");

                if (hasSql)
                    batches.Add(new Batch(script[batchStart..(lineStart + scanner.GoStart)], repetitions));
                batchStart = lineStart + scanner.LastTokenEnd;
                hasSql = false;
            }
            else
            {
                hasSql |= scanner.TokenCount > 0;
            }

            lineStart += lineLength;
            if (lineStart < script.Length && script[lineStart++] == '\r'
                && lineStart < script.Length && script[lineStart] == '\n')
                lineStart++;
        }

        if (hasSql)
            batches.Add(new Batch(script[batchStart..], 1));
        return batches;
    }

    private sealed class LineScanner
    {
        private int commentDepth;
        private char quoteEnd;

        public int GoStart { get; private set; }
        public int TokenCount { get; private set; }
        public int LastTokenStart { get; private set; }
        public int LastTokenEnd { get; private set; }

        public void Scan(ReadOnlySpan<char> line)
        {
            GoStart = -1;
            TokenCount = quoteEnd == '\0' ? 0 : 1;
            for (var index = 0; index < line.Length;)
            {
                var current = line[index];
                var next = index + 1 < line.Length ? line[index + 1] : '\0';
                if (quoteEnd != '\0')
                {
                    index++;
                    if (current == quoteEnd)
                    {
                        if (next == quoteEnd)
                            index++;
                        else
                            quoteEnd = '\0';
                    }
                }
                else if (commentDepth > 0)
                {
                    if (current == '/' && next == '*')
                    {
                        commentDepth++;
                        index += 2;
                    }
                    else if (current == '*' && next == '/')
                    {
                        commentDepth--;
                        index += 2;
                    }
                    else
                        index++;
                }
                else if (current == '-' && next == '-')
                    break;
                else if (current == '/' && next == '*')
                {
                    commentDepth = 1;
                    index += 2;
                }
                else if (char.IsWhiteSpace(current))
                    index++;
                else
                {
                    LastTokenStart = index;
                    TokenCount++;
                    if (current is '\'' or '"' or '[')
                    {
                        quoteEnd = current == '[' ? ']' : current;
                        index++;
                    }
                    else
                    {
                        index++;
                        while (index < line.Length && !StartsTriviaOrQuote(line[index..]))
                            index++;
                        if (TokenCount == 1 && line[LastTokenStart..index].Equals("GO", StringComparison.OrdinalIgnoreCase))
                            GoStart = LastTokenStart;
                    }
                    LastTokenEnd = index;
                }
            }
        }

        private static bool StartsTriviaOrQuote(ReadOnlySpan<char> text)
            => char.IsWhiteSpace(text[0]) || text[0] is '\'' or '"' or '['
                || text.StartsWith("--", StringComparison.Ordinal) || text.StartsWith("/*", StringComparison.Ordinal);
    }
}
