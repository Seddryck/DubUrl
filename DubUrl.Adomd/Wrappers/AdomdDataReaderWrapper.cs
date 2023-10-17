using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Wrappers
{
    internal class AdomdDataReaderWrapper : DbDataReader, IDataReader
    {
        protected AdomdDataReader InnerDataReader { get; }

        public AdomdDataReaderWrapper(AdomdDataReader innerDataReader)
            => InnerDataReader = innerDataReader;

        public override void Close()
            => InnerDataReader.Close();

        public override int Depth => 0;

        public override int FieldCount
            => InnerDataReader.FieldCount;

        public override bool HasRows
            => throw new NotImplementedException();

        public override bool IsClosed
            => InnerDataReader.IsClosed;

        public override int RecordsAffected
            => throw new NotSupportedException();

        public override object this[string name]
            => InnerDataReader[name];

        public override object this[int ordinal]
            => InnerDataReader[ordinal];

        public override bool GetBoolean(int ordinal)
            => InnerDataReader.GetBoolean(ordinal);
        public override byte GetByte(int ordinal)
            => InnerDataReader.GetByte(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
            => throw new NotSupportedException();
        public override char GetChar(int ordinal)
            => InnerDataReader.GetChar(ordinal);
        public override long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal)
            => InnerDataReader.GetDataTypeName(ordinal);
        public override DateTime GetDateTime(int ordinal)
            => InnerDataReader.GetDateTime(ordinal);
        public override decimal GetDecimal(int ordinal)
            => InnerDataReader.GetDecimal(ordinal);
        public override double GetDouble(int ordinal)
            => InnerDataReader.GetDouble(ordinal);
        public override IEnumerator GetEnumerator()
            => InnerDataReader.GetEnumerator();
        public override Type GetFieldType(int ordinal)
            => InnerDataReader.GetFieldType(ordinal);
        public override float GetFloat(int ordinal)
            => InnerDataReader.GetFloat(ordinal);
        public override Guid GetGuid(int ordinal)
            => InnerDataReader.GetGuid(ordinal);
        public override short GetInt16(int ordinal)
        => InnerDataReader.GetInt16(ordinal);   
        public override int GetInt32(int ordinal)
            => InnerDataReader.GetInt32(ordinal);
        public override long GetInt64(int ordinal)
            => InnerDataReader.GetInt64(ordinal);
        public override string GetName(int ordinal)
        {
            var fieldName = InnerDataReader.GetName(ordinal);
            return fieldName.StartsWith("[") && fieldName.EndsWith("]")
                ? fieldName[1..^1]
                : fieldName;
        }

        public override int GetOrdinal(string name)
            => InnerDataReader.GetOrdinal(name);
        public override string GetString(int ordinal)
            => InnerDataReader.GetString(ordinal);
        public override object GetValue(int ordinal)
            => InnerDataReader.GetValue(ordinal);
        public override int GetValues(object[] values)
            => InnerDataReader.GetValues(values);
        public override bool IsDBNull(int ordinal)
            => InnerDataReader.IsDBNull(ordinal);
        public override bool NextResult()
            => InnerDataReader.NextResult();
        public override bool Read()
            => InnerDataReader.Read();
    }
}
