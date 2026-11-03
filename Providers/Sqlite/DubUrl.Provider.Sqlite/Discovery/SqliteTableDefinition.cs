namespace DubUrl.Provider.Sqlite.Discovery;

/// <summary>Extracts expression boundaries, never interprets catalog SQL as portable SQL.</summary>
internal sealed class SqliteTableDefinition
{
    // PRAGMA preserves declaration spelling. SQLite does not fold non-ASCII case pairs.
    internal Dictionary<string, string> Generated { get; } = new(StringComparer.Ordinal);
    internal List<(string? Name, string Expression)> Checks { get; } = [];

    internal static SqliteTableDefinition Parse(string? sql)
    {
        var result = new SqliteTableDefinition();
        if (sql is null) return result;
        var tokens = Tokenize(sql);
        var open = tokens.FindIndex(token => token.Text == "(");
        if (open < 0) return result;
        var depth = 0;
        var start = open + 1;
        for (var i = start; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (depth == 0 && token.Text is ")" or ",")
            {
                ReadPart(tokens.GetRange(start, i - start), result);
                start = i + 1;
                if (token.Text == ")") break;
            }
            if (token.Text == "(") depth++;
            if (token.Text == ")") depth--;
        }
        return result;
    }

    private static void ReadPart(List<Token> tokens, SqliteTableDefinition result)
    {
        if (tokens.Count == 0) return;
        var depth = 0;
        for (var i = tokens[0].Keyword("CHECK") ? 0 : 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (depth == 0 && IsExpressionStart(tokens, i))
            {
                var end = ExpressionEnd(tokens, i + 1);
                if (end == tokens.Count) return;
                var expression = ExpressionText(tokens, i + 2, end);
                if (token.Keyword("CHECK"))
                    result.Checks.Add((ConstraintName(tokens, i), expression));
                else result.Generated[tokens[0].Identifier] = expression;
                i = end;
                continue;
            }
            if (token.Text == "(") depth++;
            if (token.Text == ")") depth--;
        }
    }

    private static bool IsExpressionStart(List<Token> tokens, int index)
        => (tokens[index].Keyword("CHECK") || tokens[index].Keyword("AS"))
            && index + 1 < tokens.Count && tokens[index + 1].Text == "(";

    private static string? ConstraintName(List<Token> tokens, int index)
        => index >= 2 && tokens[index - 2].Keyword("CONSTRAINT") ? tokens[index - 1].Identifier : null;

    private static int ExpressionEnd(List<Token> tokens, int open)
    {
        var depth = 0;
        for (var i = open; i < tokens.Count; i++)
        {
            if (tokens[i].Text == "(") depth++;
            if (tokens[i].Text == ")" && --depth == 0) return i;
        }
        return tokens.Count;
    }

    private static string ExpressionText(List<Token> tokens, int start, int end)
    {
        // Remove comments while retaining adjacency within numeric literals and quoted values.
        var parts = tokens.Skip(start).Take(end - start).ToArray();
        return string.Concat(parts.Select((part, index) =>
            (index > 0 && part.Start > parts[index - 1].Start + parts[index - 1].Text.Length ? " " : "") + part.Text));
    }

    private sealed record Token(string Text, bool Quoted, int Start = 0)
    {
        internal bool Keyword(string word) => !Quoted && Text.Equals(word, StringComparison.OrdinalIgnoreCase);
        internal string Identifier => Quoted ? Text[1..^1].Replace(new string(Text[^1], 2), Text[^1].ToString(), StringComparison.Ordinal) : Text;
    }

    private static List<Token> Tokenize(string sql)
    {
        var tokens = new List<Token>();
        for (var i = 0; i < sql.Length;)
        {
            if (SkipTrivia(sql, ref i)) continue;
            if (i < 0) return [];
            var token = ReadToken(sql, ref i);
            if (token is null) return [];
            tokens.Add(token);
        }
        return tokens;
    }

    private static bool SkipTrivia(string sql, ref int position)
    {
        if (char.IsWhiteSpace(sql[position])) { position++; return true; }
        if (sql.AsSpan(position).StartsWith("--"))
        { while (position < sql.Length && sql[position] != '\n') position++; return true; }
        if (!sql.AsSpan(position).StartsWith("/*")) return false;
        var end = sql.IndexOf("*/", position + 2, StringComparison.Ordinal);
        position = end < 0 ? -1 : end + 2;
        return end >= 0;
    }

    private static Token? ReadToken(string sql, ref int position)
    {
        var start = position++;
        var quoted = sql[start] is '\'' or '"' or '`' or '[';
        if (quoted)
        {
            position = QuotedEnd(sql, start);
            if (position < 0) return null;
        }
        else if (IsWordCharacter(sql[start]))
            while (position < sql.Length && (IsWordCharacter(sql[position]) || sql[position] == '.')) position++;
        else if (IsCompoundOperator(sql, start)) position++;
        return new(sql[start..position], quoted, start);
    }

    private static bool IsWordCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '_' or '$';

    private static bool IsCompoundOperator(string sql, int start)
        => start + 1 < sql.Length && sql[start] is '<' or '>' or '!' && sql[start + 1] is '=' or '>';

    private static int QuotedEnd(string sql, int start)
    {
        var close = sql[start] == '[' ? ']' : sql[start];
        var position = start + 1;
        while (position < sql.Length)
        {
            if (sql[position++] != close) continue;
            if (position < sql.Length && sql[position] == close) { position++; continue; }
            return position;
        }
        return -1;
    }
}
