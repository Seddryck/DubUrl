using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Wrappers
{
    internal class AdomdParameterCollectionWrapper : DbParameterCollection
    {
        protected AdomdCommand Command { get; }
        protected AdomdParameterCollection InnerCollection { get => Command.Parameters; }

        public override int Count
            => InnerCollection.Count;

        public override object SyncRoot
            => throw new NotImplementedException();

        public AdomdParameterCollectionWrapper(AdomdCommand command)
            => Command = command;

        public override int Add(object value)
        {
            ValidateType(value);
            InnerCollection.Add(((AdomdParameterWrapper)value).InnerParameter);
            return Count - 1;
        }
        private static void ValidateType(object? value)
        {
            if (!typeof(AdomdParameterWrapper).IsInstanceOfType(value ?? throw new ArgumentNullException(nameof(value))))
                throw new ArgumentException("wrong type", nameof(value));
        }
        public override void AddRange(Array values)
            => throw new NotImplementedException();
        public override void Clear()
            => InnerCollection.Clear();
        public override bool Contains(object value)
            => throw new NotImplementedException();
        public override bool Contains(string value)
            => InnerCollection.Contains(value);
        public override void CopyTo(Array array, int index)
            => throw new NotImplementedException();
        public override IEnumerator GetEnumerator()
            => throw new NotImplementedException();
        protected override DbParameter GetParameter(int index)
            => throw new NotImplementedException();
        protected override DbParameter GetParameter(string parameterName)
            => throw new NotImplementedException();
        public override int IndexOf(object value)
            => throw new NotImplementedException();
        public override int IndexOf(string parameterName)
            => InnerCollection.IndexOf(parameterName);
        public override void Insert(int index, object value)
        {
            ValidateType(value);
            InnerCollection.Insert(index, ((AdomdParameterWrapper)value).InnerParameter);
        }

        public override void Remove(object value)
        {
            ValidateType(value);
            InnerCollection.Remove(((AdomdParameterWrapper)value).InnerParameter);
        }
        public override void RemoveAt(int index)
            => InnerCollection.RemoveAt(index);
        public override void RemoveAt(string parameterName)
            => InnerCollection.RemoveAt(parameterName);
        protected override void SetParameter(int index, DbParameter value)
            => throw new NotSupportedException();
        protected override void SetParameter(string parameterName, DbParameter value)
            => throw new NotSupportedException();
    }
}
