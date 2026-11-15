using System.Collections.Immutable;

namespace DubUrl.Parser;

/// <summary>A node in a parsed SQL check expression.</summary>
public abstract record SqlExpression
{
    private SqlExpression() { }

    public sealed record BinaryOp(SqlExpression Left, SqlBinaryOperator Operator, SqlExpression Right) : SqlExpression;

    public sealed record IsNull(SqlExpression Operand) : SqlExpression;

    public sealed record IsNotNull(SqlExpression Operand) : SqlExpression;

    /// <summary>An identifier whose parts are ordered from qualifier to column name.</summary>
    public sealed record Identifier(ImmutableArray<SqlIdentifierPart> Parts) : SqlExpression;

    public abstract record Literal : SqlExpression
    {
        private Literal() { }

        /// <summary>The original numeric text, including any sign, exponent, and precision.</summary>
        public sealed record Number(string Text) : Literal;

        public sealed record String(string Value, bool IsNational = false) : Literal;

        /// <summary>A string literal with an explicit SQL type, such as DATE.</summary>
        public sealed record TypedString(string SqlType, string Value) : Literal;

        public sealed record Boolean(bool Value) : Literal;

        public sealed record Null : Literal;
    }
}

/// <summary>The decoded text of an identifier part and its opening quote character, if quoted.</summary>
public sealed record SqlIdentifierPart(string Name, char? QuoteStyle);
