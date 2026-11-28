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
                var remaining = line[index..];
                if (quoteEnd != '\0')
                    index += ReadQuotedText(remaining);
                else if (commentDepth > 0)
                    index += ReadBlockComment(remaining);
                else if (remaining.StartsWith("--", StringComparison.Ordinal))
                    break;
                else if (remaining.StartsWith("/*", StringComparison.Ordinal))
                {
                    commentDepth = 1;
                    index += 2;
                }
                else if (char.IsWhiteSpace(remaining[0]))
                    index++;
                else
                    index = ScanToken(line, index);
            }
        }

        private int ScanToken(ReadOnlySpan<char> line, int start)
        {
            var end = start + 1;
            var first = line[start];
            TokenCount++;

            if (first is '\'' or '"' or '[')
                quoteEnd = first == '[' ? ']' : first;
            else
            {
                while (end < line.Length && !StartsTriviaOrQuote(line[end..]))
                    end++;
                if (TokenCount == 1 && line[start..end].Equals("GO", StringComparison.OrdinalIgnoreCase))
                    GoStart = start;
            }

            LastTokenStart = start;
            LastTokenEnd = end;
            return end;
        }

        private int ReadQuotedText(ReadOnlySpan<char> text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] != quoteEnd)
                    continue;
                if (index + 1 < text.Length && text[index + 1] == quoteEnd)
                {
                    index++;
                    continue;
                }

                quoteEnd = '\0';
                return index + 1;
            }

            return text.Length;
        }

        private int ReadBlockComment(ReadOnlySpan<char> text)
        {
            var index = 0;
            while (index < text.Length && commentDepth > 0)
            {
                var remaining = text[index..];
                if (remaining.StartsWith("/*", StringComparison.Ordinal))
                {
                    commentDepth++;
                    index += 2;
                }
                else if (remaining.StartsWith("*/", StringComparison.Ordinal))
                {
                    commentDepth--;
                    index += 2;
                }
                else
                    index++;
            }

            return index;
        }

        private static bool StartsTriviaOrQuote(ReadOnlySpan<char> text)
            => char.IsWhiteSpace(text[0]) || text[0] is '\'' or '"' or '['
                || text.StartsWith("--", StringComparison.Ordinal) || text.StartsWith("/*", StringComparison.Ordinal);
    }
}
