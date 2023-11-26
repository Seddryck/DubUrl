using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Extensions;
using DubUrl.Querying.Parametrizing;
using NUnit.Framework;

namespace DubUrl.Testing.Querying.Parametrizing;

public class DubUrlParameterFactoryTest
{
    [Test]
    [TestCase(true, typeof(bool), typeof(DubUrlParameterBoolean))]
    [TestCase(5, typeof(sbyte), typeof(DubUrlParameterTinyInt))]
    [TestCase(5, typeof(short), typeof(DubUrlParameterSmallInt))]
    [TestCase(5, typeof(int), typeof(DubUrlParameterInt))]
    [TestCase(5, typeof(long), typeof(DubUrlParameterBigInt))]
    [TestCase(5, typeof(byte), typeof(DubUrlParameterTinyIntUnsigned))]
    [TestCase(5, typeof(ushort), typeof(DubUrlParameterSmallIntUnsigned))]
    [TestCase(5, typeof(uint), typeof(DubUrlParameterIntUnsigned))]
    [TestCase(5, typeof(ulong), typeof(DubUrlParameterBigIntUnsigned))]
    [TestCase(5.01, typeof(float), typeof(DubUrlParameterSingle))]
    [TestCase(5.01, typeof(double), typeof(DubUrlParameterDouble))]
    [TestCase(5.01, typeof(decimal), typeof(DubUrlParameterDecimal))]
    [TestCase("00000000-0000-0000-0000-000000000000", typeof(Guid), typeof(DubUrlParameterGuid))]
    [TestCase("2023-11-26T13:02:00", typeof(DateTime), typeof(DubUrlParameterDateTimeHighPrecision))]
    [TestCase("2023-11-26T13:02:00Z", typeof(DateTimeOffset), typeof(DubUrlParameterDateTimeOffset))]
    [TestCase("2023-11-26", typeof(DateOnly), typeof(DubUrlParameterDate))]
    [TestCase("13:02:00", typeof(TimeOnly), typeof(DubUrlParameterTime))]
    [TestCase("foobar", typeof(string), typeof(DubUrlParameterStringAnsi))]
    [TestCase("foobar", typeof(byte[]), typeof(DubUrlParameterBinary))]
    [TestCase("foobar", typeof(object), typeof(DubUrlParameterObject))]
    public void Instantiate(object source, Type type, Type expected)
    {
        var value = source switch
        {
            string _ when type == typeof(byte[]) => "foobar"u8.ToArray(),
            string _ when type == typeof(object) => new object(),
            string x when type == typeof(Guid) => Guid.Parse(x),
            string dt when type == typeof(TimeOnly) => TimeOnly.Parse(dt),
            string dt when type == typeof(DateOnly) => DateOnly.Parse(dt),
            string dt when type == typeof(DateTimeOffset) => DateTimeOffset.Parse(dt),
            _ => Convert.ChangeType(source, type)
        };
        var factory = new DubUrlParameterFactory();
        var parameter = factory.Instantiate("param01", value);
        Assert.Multiple(() =>
        {
            Assert.That(parameter, Is.TypeOf(expected));
            Assert.That(parameter.Name, Is.EqualTo("param01"));
            Assert.That(parameter.Value, Is.EqualTo(value));
        });
    }
}
