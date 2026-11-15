using NUnit.Framework;
using static DubUrl.Parser.SqlExpression;

namespace DubUrl.Parser.Testing;

public class SqlParserAdapterTests
{
    [TestCase(SqlDialect.Generic)]
    [TestCase(SqlDialect.DuckDb)]
    [TestCase(SqlDialect.PostgreSql)]
    [TestCase(SqlDialect.SqlServer)]
    public void ParseCheckExpression_WorkOrderCheck_PreservesPredicate(SqlDialect dialect)
    {
        const string sql = """(("EndDate" >= "StartDate") OR ("EndDate" IS NULL))""";

        var disjunction = (BinaryOp)SqlParserAdapter.ParseCheckExpression(sql, dialect);
        var comparison = (BinaryOp)disjunction.Left;
        var nullTest = (IsNull)disjunction.Right;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(disjunction.Operator, Is.EqualTo(SqlBinaryOperator.Or));
            Assert.That(comparison.Operator, Is.EqualTo(SqlBinaryOperator.GreaterThanOrEqual));
            Assert.That(((Identifier)comparison.Left).Parts, Is.EqualTo(new[] { new SqlIdentifierPart("EndDate", '"') }));
            Assert.That(((Identifier)comparison.Right).Parts, Is.EqualTo(new[] { new SqlIdentifierPart("StartDate", '"') }));
            Assert.That(((Identifier)nullTest.Operand).Parts, Is.EqualTo(new[] { new SqlIdentifierPart("EndDate", '"') }));
        }
    }

    [TestCase("=", SqlBinaryOperator.Equal)]
    [TestCase("!=", SqlBinaryOperator.NotEqual)]
    [TestCase("<>", SqlBinaryOperator.NotEqual)]
    [TestCase("<", SqlBinaryOperator.LessThan)]
    [TestCase(">", SqlBinaryOperator.GreaterThan)]
    [TestCase("<=", SqlBinaryOperator.LessThanOrEqual)]
    [TestCase(">=", SqlBinaryOperator.GreaterThanOrEqual)]
    public void ParseCheckExpression_Comparison_MapsOperator(string sqlOperator, SqlBinaryOperator expected)
    {
        var comparison = (BinaryOp)SqlParserAdapter.ParseCheckExpression($"Quantity {sqlOperator} 1");

        Assert.That(comparison.Operator, Is.EqualTo(expected));
    }

    [TestCase("A = 1 OR B = 2 AND C = 3", SqlBinaryOperator.Or, SqlBinaryOperator.And)]
    [TestCase("(A = 1 OR B = 2) AND C = 3", SqlBinaryOperator.And, SqlBinaryOperator.Or)]
    public void ParseCheckExpression_LogicalOperators_RespectPrecedence(
        string sql, SqlBinaryOperator outerOperator, SqlBinaryOperator innerOperator)
    {
        var outer = (BinaryOp)SqlParserAdapter.ParseCheckExpression(sql);
        var inner = (BinaryOp)(outerOperator == SqlBinaryOperator.Or ? outer.Right : outer.Left);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outer.Operator, Is.EqualTo(outerOperator));
            Assert.That(inner.Operator, Is.EqualTo(innerOperator));
        }
    }

    [Test]
    public void ParseCheckExpression_IsNotNull_PreservesQuotedIdentifierParts()
    {
        const string sql = """[Production.WorkOrder].[End]]Date] IS NOT NULL""";

        var nullTest = (IsNotNull)SqlParserAdapter.ParseCheckExpression(sql, SqlDialect.SqlServer);
        var identifier = (Identifier)nullTest.Operand;

        Assert.That(identifier.Parts, Is.EqualTo(new[]
        {
            new SqlIdentifierPart("Production.WorkOrder", '['),
            new SqlIdentifierPart("End]Date", '['),
        }));
    }

    [TestCase("0")]
    [TestCase("+17")]
    [TestCase("-123456789012345678901234567890.123456789")]
    [TestCase("1.1e-10")]
    public void ParseCheckExpression_Number_PreservesNumericText(string number)
    {
        var comparison = (BinaryOp)SqlParserAdapter.ParseCheckExpression($"Amount >= {number}");

        Assert.That(comparison.Right, Is.EqualTo(new Literal.Number(number)));
    }

    [Test]
    public void ParseCheckExpression_String_DecodesEscapedQuote()
    {
        var comparison = (BinaryOp)SqlParserAdapter.ParseCheckExpression("LastName = 'O''Brien'");

        Assert.That(comparison.Right, Is.EqualTo(new Literal.String("O'Brien")));
    }

    [Test]
    public void ParseCheckExpression_NationalString_PreservesPrefix()
    {
        var comparison = (BinaryOp)SqlParserAdapter.ParseCheckExpression("Name = N'Åsa'", SqlDialect.SqlServer);

        Assert.That(comparison.Right, Is.EqualTo(new Literal.String("Åsa", IsNational: true)));
    }

    [Test]
    public void ParseCheckExpression_TypedString_PreservesSqlType()
    {
        var comparison = (BinaryOp)SqlParserAdapter.ParseCheckExpression("EndDate >= DATE '2026-11-15'", SqlDialect.DuckDb);

        Assert.That(comparison.Right, Is.EqualTo(new Literal.TypedString("DATE", "2026-11-15")));
    }

    [TestCase("TRUE", true)]
    [TestCase("FALSE", false)]
    public void ParseCheckExpression_Boolean_PreservesValue(string sql, bool expected)
        => Assert.That(SqlParserAdapter.ParseCheckExpression(sql), Is.EqualTo(new Literal.Boolean(expected)));

    [Test]
    public void ParseCheckExpression_Null_PreservesLiteral()
        => Assert.That(SqlParserAdapter.ParseCheckExpression("NULL"), Is.EqualTo(new Literal.Null()));

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("/* comment only */")]
    [TestCase("Quantity >")]
    [TestCase("Name = 'unclosed")]
    [TestCase("Quantity > 0 extra")]
    [TestCase("Quantity > 0; SELECT 1")]
    public void ParseCheckExpression_InvalidInput_ThrowsFormatException(string sql)
        => Assert.That(() => SqlParserAdapter.ParseCheckExpression(sql), Throws.TypeOf<FormatException>());

    [TestCase("ABS(Quantity) > 0")]
    [TestCase("Quantity + 1 > 0")]
    [TestCase("Quantity BETWEEN 1 AND 2")]
    [TestCase("Quantity = ?")]
    public void ParseCheckExpression_UnsupportedSyntax_ThrowsNotSupportedException(string sql)
        => Assert.That(() => SqlParserAdapter.ParseCheckExpression(sql), Throws.TypeOf<NotSupportedException>());
}
