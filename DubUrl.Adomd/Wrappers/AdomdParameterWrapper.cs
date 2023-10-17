﻿using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Wrappers
{
    internal class AdomdParameterWrapper : DbParameter, IDbDataParameter, IDataParameter
    {
        internal AdomdParameter InnerParameter { get; }
        
        public AdomdParameterWrapper()
            => InnerParameter = new AdomdParameter();

        public override DbType DbType 
        { 
            get => InnerParameter.DbType; 
            set => InnerParameter.DbType =  value; 
        }
        public override ParameterDirection Direction 
        {
            get => InnerParameter.Direction;
            set => InnerParameter.Direction = value;
        }
        public override bool IsNullable
        {
            get => InnerParameter.IsNullable;
            set => InnerParameter.IsNullable = value;
        }
        [AllowNull]
        public override string ParameterName
        {
            get => InnerParameter.ParameterName;
            set => InnerParameter.ParameterName = value;
        }
        public override int Size
        {
            get => InnerParameter.Size;
            set => InnerParameter.Size = value;
        }
        [AllowNull]
        public override string SourceColumn
        {
            get => InnerParameter.SourceColumn;
            set => InnerParameter.SourceColumn = value;
        }
        public override bool SourceColumnNullMapping
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override object? Value
        {
            get => InnerParameter.Value;
            set => InnerParameter.Value = value;
        }

        public override void ResetDbType()
            => InnerParameter.DbType = DbType.Object;
    }
}
