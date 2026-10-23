using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DubUrl.Discovery;

/// <summary>Conservative catalog-expression recognition. Never accepts raw SQL as portable expressions.</summary>
public static class DiscoveryExpressions
{
    public static bool TryLiteral(string sql, out object? value)
    {
        value = null;
        var text = sql.Trim();
        while (text.Length > 1 && text[0] == '(' && text[^1] == ')' && BalancedOuter(text))
            text = text[1..^1].Trim();
        // PostgreSQL's catalog casts of literal values; the entire suffix must be a known scalar type.
        text = Regex.Replace(text, @"::(?:character varying|text|integer|bigint|smallint|numeric|boolean|date|timestamp without time zone)$", "", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        while (text.Length > 1 && text[0] == '(' && text[^1] == ')' && BalancedOuter(text)) text = text[1..^1].Trim();
        if (string.Equals(text, "NULL", StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(text, @"^N?'(?:[^']|'')*'$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        { value = text[(text[0] == 'N' ? 2 : 1)..^1].Replace("''", "'", StringComparison.Ordinal); return true; }
        if (bool.TryParse(text, out var boolean)) { value = boolean; return true; }
        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
        { value = integer is >= int.MinValue and <= int.MaxValue ? (object)(int)integer : integer; return true; }
        if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
        { value = number; return true; }
        return false;
    }
    public static bool TryExpression(string sql, out MetadataExpression? expression)
    {
        expression = null;
        var text = sql.Trim();
        while (text.Length > 1 && text[0] == '(' && text[^1] == ')' && BalancedOuter(text)) text = text[1..^1].Trim();
        if (TryLiteral(text, out var value)) { expression = new("value", Value: value); return true; }
        var function = Regex.Match(text, @"^(upper|lower|length)\s*\((.+)\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (function.Success && TryExpression(function.Groups[2].Value, out var argument))
        { expression = new("function", function.Groups[1].Value.ToLowerInvariant() switch { "upper" => "Upper", "lower" => "Lower", _ => "Length" }, Arguments: [argument!]); return true; }
        try
        {
            var name = MetadataNames.Parse(text);
            if (name.Schema is null) { expression = new("column", name.Name); return true; }
        }
        catch (ArgumentException) { }
        return false;
    }
    public static bool TryCheck(string sql, string? name, out MetadataCheck? check)
    {
        check = null;
        var text = sql.Trim();
        while (text.Length > 1 && text[0] == '(' && text[^1] == ')' && BalancedOuter(text)) text = text[1..^1].Trim();
        var match = Regex.Match(text, @"^(.+?)\s*(<>|!=|<=|>=|=|<|>)\s*(.+)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success || !TryExpression(match.Groups[1].Value, out var left) || !TryExpression(match.Groups[3].Value, out var right)) return false;
        check = new(name, left!, match.Groups[2].Value, right!);
        return true;
    }

    private static bool BalancedOuter(string text)
    {
        var depth = 0;
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\'')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '\'') { i++; continue; }
                quoted = !quoted;
            }
            if (quoted) continue;
            if (text[i] == '(') depth++;
            if (text[i] == ')' && --depth == 0 && i != text.Length - 1) return false;
        }
        return depth == 0 && !quoted;
    }
}

/// <summary>Parses one to four dot-separated identifier components with SQL identifier quoting.</summary>
public static class MetadataNames
{
    public static MetadataName Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = new List<string>();
        var position = 0;
        while (position < text.Length)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
            if (position == text.Length) throw new ArgumentException("An identifier component is required.");
            var part = text[position] is '"' or '[' or '`'
                ? ReadQuotedPart(text, ref position) : ReadUnquotedPart(text, ref position);
            parts.Add(part);
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
            if (position == text.Length) break;
            if (text[position++] != '.' || position == text.Length) throw new ArgumentException("Invalid qualified identifier.");
        }
        if (parts.Count is < 1 or > 4) throw new ArgumentException("Identifiers support one to four components.");
        parts.Reverse();
        return new(parts[0], parts.ElementAtOrDefault(1), parts.ElementAtOrDefault(2), parts.ElementAtOrDefault(3));
    }

    private static string ReadQuotedPart(string text, ref int position)
    {
        var close = text[position++] == '[' ? ']' : text[position - 1];
        var buffer = new System.Text.StringBuilder();
        var closed = false;
        while (position < text.Length)
        {
            var c = text[position++];
            if (c != close) { buffer.Append(c); continue; }
            if (position < text.Length && text[position] == close) { buffer.Append(close); position++; continue; }
            closed = true; break;
        }
        if (!closed || buffer.Length == 0) throw new ArgumentException("Invalid quoted identifier.");
        return buffer.ToString();
    }

    private static string ReadUnquotedPart(string text, ref int position)
    {
        var start = position;
        while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] is '_' or '$')) position++;
        if (position == start || char.IsDigit(text[start])) throw new ArgumentException("Invalid identifier.");
        return text[start..position];
    }
}
