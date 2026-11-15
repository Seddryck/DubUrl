using System.Collections.Immutable;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using SqlParser.Tokens;
using NativeParser = SqlParser.Parser;

namespace DubUrl.Parser;

/// <summary>Parses SQL check expressions using SqlParserCS.</summary>
public static class SqlParserAdapter
{
    /// <summary>Parses a bare SQL check predicate using the supplied dialect.</summary>
    /// <exception cref="FormatException">The input is malformed or contains trailing tokens.</exception>
    /// <exception cref="NotSupportedException">The parser recognized syntax outside the supported AST.</exception>
    public static SqlExpression ParseCheckExpression(string sql, SqlDialect dialect = SqlDialect.Generic)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql))
            throw new FormatException("A SQL check expression is required.");

        var nativeDialect = dialect switch
        {
            SqlDialect.Generic => (Dialect)new GenericDialect(),
            SqlDialect.DuckDb => new DuckDbDialect(),
            SqlDialect.PostgreSql => new PostgreSqlDialect(),
            SqlDialect.SqlServer => new MsSqlDialect(),
            _ => throw new ArgumentOutOfRangeException(nameof(dialect)),
        };

        Expression expression;
        try
        {
            var parser = new NativeParser().TryWithSql(sql, nativeDialect);
            if (parser.PeekToken() is EOF)
                throw new FormatException("A SQL check expression is required.");

            expression = parser.ParseExpr();
            if (parser.PeekToken() is not EOF)
                throw new FormatException("Unexpected tokens after the SQL check expression.");
        }
        catch (Exception exception) when (exception is ParserException or TokenizeException)
        {
            throw new FormatException($"Invalid SQL check expression. {exception.Message}", exception);
        }

        return MapExpression(expression);
    }

    private static SqlExpression MapExpression(Expression expression)
        => expression switch
        {
            Expression.Nested nested => MapExpression(nested.Expression),
            Expression.BinaryOp binary => new SqlExpression.BinaryOp(
                MapExpression(binary.Left), MapOperator(binary.Op), MapExpression(binary.Right)),
            Expression.IsNull test => new SqlExpression.IsNull(MapExpression(test.Expression)),
            Expression.IsNotNull test => new SqlExpression.IsNotNull(MapExpression(test.Expression)),
            Expression.Identifier identifier => new SqlExpression.Identifier([MapIdentifierPart(identifier.Ident)]),
            Expression.CompoundIdentifier identifier => new SqlExpression.Identifier(
                identifier.Idents.Select(MapIdentifierPart).ToImmutableArray()),
            Expression.LiteralValue literal => MapLiteral(literal.Value),
            Expression.TypedString literal => new SqlExpression.Literal.TypedString(literal.DataType.ToSql(), literal.Value),
            Expression.UnaryOp
            {
                Op: UnaryOperator.Plus or UnaryOperator.Minus,
                Expression: Expression.LiteralValue { Value: Value.Number { Long: false } number },
            } signed => new SqlExpression.Literal.Number((signed.Op == UnaryOperator.Minus ? "-" : "+") + number.Value),
            _ => throw new NotSupportedException($"SQL check expression node '{expression.GetType().Name}' is unsupported."),
        };

    private static SqlIdentifierPart MapIdentifierPart(Ident identifier)
        => new(identifier.Value, identifier.QuoteStyle);

    private static SqlExpression.Literal MapLiteral(Value value)
        => value switch
        {
            Value.Number { Long: false } number => new SqlExpression.Literal.Number(number.Value),
            Value.SingleQuotedString text => new SqlExpression.Literal.String(text.Value),
            Value.NationalStringLiteral text => new SqlExpression.Literal.String(text.Value, IsNational: true),
            Value.Boolean boolean => new SqlExpression.Literal.Boolean(boolean.Value),
            Value.Null => new SqlExpression.Literal.Null(),
            _ => throw new NotSupportedException($"SQL check literal '{value.GetType().Name}' is unsupported."),
        };

    private static SqlBinaryOperator MapOperator(BinaryOperator op)
        => op switch
        {
            BinaryOperator.Or => SqlBinaryOperator.Or,
            BinaryOperator.And => SqlBinaryOperator.And,
            BinaryOperator.Eq => SqlBinaryOperator.Equal,
            BinaryOperator.NotEq => SqlBinaryOperator.NotEqual,
            BinaryOperator.Lt => SqlBinaryOperator.LessThan,
            BinaryOperator.Gt => SqlBinaryOperator.GreaterThan,
            BinaryOperator.LtEq => SqlBinaryOperator.LessThanOrEqual,
            BinaryOperator.GtEq => SqlBinaryOperator.GreaterThanOrEqual,
            _ => throw new NotSupportedException($"SQL check binary operator '{op}' is unsupported."),
        };
}
