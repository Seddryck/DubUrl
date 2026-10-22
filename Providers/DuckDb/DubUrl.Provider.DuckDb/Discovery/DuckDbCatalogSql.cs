using System.Text;
using DubUrl.Discovery;

namespace DubUrl.Provider.DuckDb.Discovery;

/// <summary>Reads the small canonical DDL fragments absent from DuckDB's structured catalogs. Never executes catalog SQL.</summary>
internal static class DuckDbCatalogSql
{
    private sealed record Token(string Value, int Start, int End, bool Quoted = false, bool Literal = false)
    {
        public bool Is(string word) => !Quoted && !Literal && Value.Equals(word, StringComparison.OrdinalIgnoreCase);
        public bool Identifier => !Literal && (Quoted || (Value.Length > 0 && (char.IsLetter(Value[0]) || Value[0] == '_')));
    }

    public static string? GeneratedExpression(string sql, string column)
    {
        var tokens = Tokenize(sql);
        var open = tokens.FindIndex(token => token.Is("("));
        if (open < 0) return null;
        var end = Closing(tokens, open);
        if (end < 0) return null;
        foreach (var (start, stop) in Items(tokens, open + 1, end))
        {
            if (!tokens[start].Identifier || tokens[start].Value != column) continue;
            var depth = 0;
            for (var i = start + 1; i < stop; i++)
            {
                if (tokens[i].Is("(")) depth++;
                else if (tokens[i].Is(")")) depth--;
                if (depth != 0 || !tokens[i].Is("GENERATED")) continue;
                var cursor = i + 1;
                if (cursor < stop && tokens[cursor].Is("ALWAYS")) cursor++;
                if (cursor + 1 >= stop || !tokens[cursor].Is("AS") || !tokens[cursor + 1].Is("(")) return "<unrepresentable generated column>";
                var close = Closing(tokens, cursor + 1);
                if (close < 0 || close >= stop) return "<unrepresentable generated column>";
                return sql[tokens[cursor + 1].End..tokens[close].Start].Trim();
            }
        }
        return null;
    }

    public static MetadataIndexColumn[]? IndexColumns(string sql)
    {
        var tokens = Tokenize(sql);
        var on = tokens.FindIndex(token => token.Is("ON"));
        if (on < 0) return null;
        var open = tokens.FindIndex(on + 1, token => token.Is("("));
        if (open < 0 || tokens.Take(open).Any(token => token.Is("USING"))) return null;
        var close = Closing(tokens, open);
        if (close < 0 || tokens.Skip(close + 1).Any(token => !token.Is(";"))) return null;
        var columns = new List<MetadataIndexColumn>();
        foreach (var (start, stop) in Items(tokens, open + 1, close))
        {
            if (!tokens[start].Identifier) return null;
            bool? descending = null;
            if (stop - start == 2)
            {
                if (tokens[start + 1].Is("DESC")) descending = true;
                else if (tokens[start + 1].Is("ASC")) descending = false;
                else return null;
            }
            else if (stop - start != 1) return null;
            columns.Add(new(tokens[start].Value, descending));
        }
        return columns.Count > 0 ? columns.ToArray() : null;
    }

    public static bool TrySequence(string expression, out MetadataName? sequence)
    {
        sequence = null;
        var tokens = Tokenize(expression);
        while (tokens.Count > 1 && tokens[0].Is("(") && Closing(tokens, 0) == tokens.Count - 1)
            tokens = tokens.GetRange(1, tokens.Count - 2);
        if (tokens.Count != 4 || !tokens[0].Is("nextval") || !tokens[1].Is("(") || !tokens[2].Literal || !tokens[3].Is(")")) return false;
        try { sequence = MetadataNames.Parse(tokens[2].Value); return true; }
        catch (ArgumentException) { return false; }
    }

    public static bool UsesSequence(string expression) => Tokenize(expression).Any(token => token.Is("nextval"));

    private static IEnumerable<(int Start, int Stop)> Items(List<Token> tokens, int start, int end)
    {
        var depth = 0;
        var cursor = start;
        for (var i = start; i < end; i++)
        {
            if (tokens[i].Is("(")) depth++;
            else if (tokens[i].Is(")")) depth--;
            else if (tokens[i].Is(",") && depth == 0)
            {
                if (i > cursor) yield return (cursor, i);
                cursor = i + 1;
            }
        }
        if (end > cursor) yield return (cursor, end);
    }

    private static int Closing(List<Token> tokens, int start)
    {
        var depth = 0;
        for (var i = start; i < tokens.Count; i++)
        {
            if (tokens[i].Is("(")) depth++;
            else if (tokens[i].Is(")") && --depth == 0) return i;
        }
        return -1;
    }

    private static List<Token> Tokenize(string sql)
    {
        var tokens = new List<Token>();
        for (var i = 0; i < sql.Length;)
        {
            if (char.IsWhiteSpace(sql[i])) { i++; continue; }
            var start = i;
            if (sql[i] is '"' or '\'')
            {
                var quote = sql[i++];
                var value = new StringBuilder();
                var closed = false;
                while (i < sql.Length)
                {
                    if (sql[i] != quote) { value.Append(sql[i++]); continue; }
                    i++;
                    if (i < sql.Length && sql[i] == quote) { value.Append(quote); i++; continue; }
                    closed = true; break;
                }
                if (!closed) return [];
                tokens.Add(new(value.ToString(), start, i, Quoted: quote == '"', Literal: quote == '\''));
            }
            else if (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '$')
            {
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '$')) i++;
                tokens.Add(new(sql[start..i], start, i));
            }
            else { i++; tokens.Add(new(sql[start..i], start, i)); }
        }
        return tokens;
    }
}
