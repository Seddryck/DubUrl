﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    public class Specificator : ISpecificator
    {
        private DbConnectionStringBuilder Csb { get; }
        public Specificator(DbConnectionStringBuilder csb)
            => (Csb) = (csb);

        public virtual void Execute(string keyword, object value)
        {
            if (!ContainsKey(keyword))
                throw new InvalidOperationException($"The keyword '{keyword}' is not valid for this type of connection string.");
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"The value for the keyword '{keyword}' cannot be null.");

            AddToken(keyword, value);
        }

        protected bool ContainsKey(string keyword) => Csb.ContainsKey(keyword);
        protected void AddToken(string keyword, object value) => Csb.Add(keyword, value);
        public string ConnectionString
        { get => Csb.ConnectionString; }

        public IReadOnlyDictionary<string, object> ToReadOnlyDictionary()
        {
            var dico = new Dictionary<string, object>();
            foreach (string key in Csb.Keys)
                dico.Add(key, Csb[key]);
            return dico;
        }
    }
}