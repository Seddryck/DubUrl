using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Extensions;

namespace DubUrl.Querying.Parametrizing;

internal class DubUrlParameterFactory : IDubUrlParameterFactory
{
    public enum Encoding
    {
        Ansi = 1,
        Unicode = 2
    }

    public DubUrlParameter Instantiate<T>(string name, T? value)
    {
        return value switch
        {
            byte[] x => new DubUrlParameterBinary(name, x),

            bool x => new DubUrlParameterBoolean(name, x),
            sbyte x => new DubUrlParameterTinyInt(name, x),
            short x => new DubUrlParameterSmallInt(name, x),
            int x => new DubUrlParameterInt(name, x),
            long x => new DubUrlParameterBigInt(name, x),
            byte x => new DubUrlParameterTinyIntUnsigned(name, x),
            ushort x => new DubUrlParameterSmallIntUnsigned(name, x),
            uint x => new DubUrlParameterIntUnsigned(name, x),
            ulong x => new DubUrlParameterBigIntUnsigned(name, x),
            float x => new DubUrlParameterSingle(name, x),
            double x => new DubUrlParameterDouble(name, x),
            decimal x => Instantiate(name, x, 30, 15),
            Guid x => new DubUrlParameterGuid(name, x),
            DateTime x => Instantiate(name, x, true),
            DateTimeOffset x => new DubUrlParameterDateTimeOffset(name, x),
            DateOnly x => new DubUrlParameterDate(name, x),
            TimeOnly x => new DubUrlParameterTime(name, x),
            string x => Instantiate(name, x, Encoding.Ansi, null),
            object x => new DubUrlParameterObject(name, x),
            _ => throw new NotImplementedException(),
        };
    }

    protected virtual DubUrlParameter Instantiate(string name, DateTime? value, bool isHighPrecision)
    {
        return isHighPrecision switch
        {
            true => new DubUrlParameterDateTimeHighPrecision(name, value),
            false => new DubUrlParameterDateTime(name, value),
        };
    }

    protected virtual DubUrlParameter Instantiate(string name, decimal? value, byte precision, byte scale)
        => new DubUrlParameterDecimal(name, precision, scale, value);

    protected virtual DubUrlParameter Instantiate(string name, string? value, Encoding encoding, int? fixedLength)
    {
        return encoding switch
        {
            Encoding.Ansi => fixedLength switch
            {
                null => new DubUrlParameterStringAnsi(name, value),
                int x => new DubUrlParameterStringAnsiFixedLength(name, x, value),
            },
            Encoding.Unicode => fixedLength switch
            {
                null => new DubUrlParameterStringUnicode(name, value),
                int x => new DubUrlParameterStringUnicodeFixedLength(name, x, value),
            },
            _ => throw new NotImplementedException()
        };
    }
}
