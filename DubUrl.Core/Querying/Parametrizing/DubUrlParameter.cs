using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing
{
    public abstract class DubUrlParameter
    {
        public object? Value { get; protected set; }
        public string Name { get; }

        public DubUrlParameter(string name)
            => Name = name;
    }

    public abstract class DubUrlParameter<T> : DubUrlParameter
    {
        public new T? Value
        { 
            get => (T?)base.Value;
            set => base.Value = value;
        }

        public DubUrlParameter(string name)
            : base(name) { }
        public DubUrlParameter(string name, T? value)
            : base(name) => Value = value;

        public void SetValue(T? value) => Value = value;
    }

    //Boolean
    [TypeMapping(DbType.Boolean)]
    public class DubUrlParameterBoolean : DubUrlParameter<bool?> 
    { 
        public DubUrlParameterBoolean(string name) : base(name) { }
        public DubUrlParameterBoolean(string name, bool? value) : base(name, value) { }
    }

    //Signed Integers
    [TypeMapping(DbType.SByte)]
    public class DubUrlParameterTinyInt : DubUrlParameter<sbyte?> 
    { 
        public DubUrlParameterTinyInt(string name) : base(name) { }
        public DubUrlParameterTinyInt(string name, sbyte? value) : base(name, value) { }
    }
    [TypeMapping(DbType.Int16)]
    public class DubUrlParameterSmallInt : DubUrlParameter<short?>

    {
        public DubUrlParameterSmallInt(string name) : base(name) { }
        public DubUrlParameterSmallInt(string name, short? value) : base(name, value) { }
    }

    [TypeMapping(DbType.Int32)] 
    public class DubUrlParameterInt : DubUrlParameter<int?> 
    { 
        public DubUrlParameterInt(string name) : base(name) { } 
        public DubUrlParameterInt(string name, int? value) : base(name, value) { }  
    }
    [TypeMapping(DbType.Int64)] 
    public class DubUrlParameterBigInt : DubUrlParameter<long?>
    {
        public DubUrlParameterBigInt(string name) : base(name) { }
        public DubUrlParameterBigInt(string name, long? value) : base(name, value) { }
    }

    //Unsigned Integers
    [TypeMapping(DbType.SByte)]
    public class DubUrlParameterTinyIntUnsigned : DubUrlParameter<byte?>
    {
        public DubUrlParameterTinyIntUnsigned(string name) : base(name) { }
        public DubUrlParameterTinyIntUnsigned(string name, byte? value) : base(name, value) { }
    }
    [TypeMapping(DbType.Int16)]
    public class DubUrlParameterSmallIntUnsigned : DubUrlParameter<ushort?>
    {
        public DubUrlParameterSmallIntUnsigned(string name) : base(name) { }
        public DubUrlParameterSmallIntUnsigned(string name, ushort? value) : base(name, value) { }
    }
    [TypeMapping(DbType.Int32)]
    public class DubUrlParameterIntUnsigned : DubUrlParameter<uint?>
    {
        public DubUrlParameterIntUnsigned(string name) : base(name) { }
        public DubUrlParameterIntUnsigned(string name, uint? value) : base(name, value) { }
    }
    [TypeMapping(DbType.Int64)]
    public class DubUrlParameterBigIntUnsigned : DubUrlParameter<ulong?>
    {
        public DubUrlParameterBigIntUnsigned(string name) : base(name) { }
        public DubUrlParameterBigIntUnsigned(string name, ulong? value) : base(name, value) { }
    }

    //Floatings
    [TypeMapping(DbType.Single)]
    public class DubUrlParameterSingle : DubUrlParameter<float?>
    {
        public DubUrlParameterSingle(string name) : base(name) { }
        public DubUrlParameterSingle(string name, float? value) : base(name, value) { }
    }
    [TypeMapping(DbType.Double)] 
    public class DubUrlParameterDouble : DubUrlParameter<double?>
    {
        public DubUrlParameterDouble(string name) : base(name) { }
        public DubUrlParameterDouble(string name, double? value) : base(name, value) { }
    }

    //Decimal
    [TypeMapping(DbType.Decimal)]
    public class DubUrlParameterDecimal : DubUrlParameter<decimal?>, IParameterPrecisionable
    {
        public byte Precision { get; }
        public byte Scale { get; }
        public DubUrlParameterDecimal(string name, byte precision, byte scale) : base(name)
            => (Precision, Scale) = (precision, scale);
        public DubUrlParameterDecimal(string name, byte precision, byte scale, decimal? value) : base(name, value)
            => (Precision, Scale) = (precision, scale);
    }

    [TypeMapping(DbType.Currency)]
    public class DubUrlParameterCurrency : DubUrlParameter<decimal?>
    {
        public DubUrlParameterCurrency(string name) : base(name) { }
        public DubUrlParameterCurrency(string name, decimal? value) : base(name, value) { }
    }

    //Guid
    [TypeMapping(DbType.Guid)]
    public class DubUrlParameterGuid : DubUrlParameter<Guid?>
    {
        public DubUrlParameterGuid(string name) : base(name) { }
        public DubUrlParameterGuid(string name, Guid? value) : base(name, value) { }
    }

    //Strings
    [TypeMapping(DbType.AnsiStringFixedLength)]
    public class DubUrlParameterStringAnsiFixedLength : DubUrlParameter<string?>, IParameterSizable
    {
        public int Size { get; }
        public DubUrlParameterStringAnsiFixedLength(string name, int size) : base(name) => Size = size;
        public DubUrlParameterStringAnsiFixedLength(string name, int size, string? value) : base(name, value) => Size = size;
    }
    [TypeMapping(DbType.AnsiString)]
    public class DubUrlParameterStringAnsi : DubUrlParameter<string>
    {
        public DubUrlParameterStringAnsi(string name) : base(name) { }
        public DubUrlParameterStringAnsi(string name, string? value) : base(name, value) { }
    }

    [TypeMapping(DbType.StringFixedLength)]
    public class DubUrlParameterStringUnicodeFixedLength : DubUrlParameter<string?>, IParameterSizable
    {
        public int Size { get; }
        public DubUrlParameterStringUnicodeFixedLength(string name, int size) : base(name) => Size = size;
        public DubUrlParameterStringUnicodeFixedLength(string name, int size, string? value) : base(name, value) => Size = size;
    }
    [TypeMapping(DbType.String)]
    public class DubUrlParameterStringUnicode : DubUrlParameter<string>
    {
        public DubUrlParameterStringUnicode(string name) : base(name) { }
        public DubUrlParameterStringUnicode(string name, string? value) : base(name, value) { }
    }

    //Date & Time
    [TypeMapping(DbType.DateTime)]
    public class DubUrlParameterDateTime : DubUrlParameter<DateTime?>
    {
        public DubUrlParameterDateTime(string name) : base(name) { }
        public DubUrlParameterDateTime(string name, DateTime? value) : base(name, value) { }
    }
    
    [TypeMapping(DbType.DateTime2)]
    public class DubUrlParameterDateTimeHighPrecision : DubUrlParameter<DateTime?>
    {
        public DubUrlParameterDateTimeHighPrecision(string name) : base(name) { }
        public DubUrlParameterDateTimeHighPrecision(string name, DateTime? value) : base(name, value) { }
    }

    [TypeMapping(DbType.DateTimeOffset)]
    public class DubUrlParameterDateTimeOffset : DubUrlParameter<DateTimeOffset?>
    {
        public DubUrlParameterDateTimeOffset(string name) : base(name) { }
        public DubUrlParameterDateTimeOffset(string name, DateTimeOffset? value) : base(name, value) { }
    }

    [TypeMapping(DbType.Date)]
    public class DubUrlParameterDate : DubUrlParameter<DateOnly?>
    {
        public DubUrlParameterDate(string name) : base(name) { }
        public DubUrlParameterDate(string name, DateOnly? value) : base(name, value) { }
    }

    [TypeMapping(DbType.Time)]
    public class DubUrlParameterTime : DubUrlParameter<TimeOnly?>

    {
        public DubUrlParameterTime(string name) : base(name) { }
        public DubUrlParameterTime(string name, TimeOnly? value) : base(name, value) { }
    }

    //Object
    [TypeMapping(DbType.Object)]
    public class DubUrlParameterObject : DubUrlParameter<object?>
    {
        public DubUrlParameterObject(string name) : base(name) { }
        public DubUrlParameterObject(string name, object? value) : base(name, value) { }
    }

    //Binary
    [TypeMapping(DbType.Binary)]
    public class DubUrlParameterBinary : DubUrlParameter<byte[]?>
    {
        public DubUrlParameterBinary(string name) : base(name) { }
        public DubUrlParameterBinary(string name, byte[]? value) : base(name, value) { }
    }
}
