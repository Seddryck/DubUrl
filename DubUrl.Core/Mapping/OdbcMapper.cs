﻿using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class OdbcMapper : BaseMapper
    {
        public OdbcMapper(DbConnectionStringBuilder csb) : base(csb) { }

        public override void ExecuteSpecific(UrlInfo urlInfo)
        {
            Specify("Server", urlInfo.Host);
            if (urlInfo.Port > 0)
                Specify("Port", urlInfo.Port);
            ExecuteAuthentification(urlInfo.Username, urlInfo.Password);
            ExecuteInitialCatalog(urlInfo.Segments);
        }

        protected internal void ExecuteAuthentification(string username, string password)
        {
            if (!string.IsNullOrEmpty(username))
                Specify("Uid", username);
            if (!string.IsNullOrEmpty(password))
                Specify("Pwd", password);
        }

        protected internal void ExecuteInitialCatalog(string[] segments)
        {
            if (segments.Length == 1)
                Specify("Database", segments.First());
            else
                throw new ArgumentOutOfRangeException();
        }

        protected override void ExecuteOptions(IDictionary<string, string> options)
        {
            foreach (var option in options)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(option.Key,"driver"))               
                {
                    if (!option.Value.StartsWith("{") || !option.Value.EndsWith("}"))
                        if (option.Value.StartsWith("{") ^ option.Value.EndsWith("}"))
                            throw new ArgumentOutOfRangeException($"The value of the option 'driver' must start with a '{{' and end with '}}' or both should be missing. The value was '{option.Value}'");
                        else
                            Specify(option.Key, $"{{{option.Value}}}");
                    else
                        Specify(option.Key, $"{option.Value}");
                }
                else
                    Specify(option.Key, option.Value);
            }
        }

        protected override void Specify(string keyword, object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"The value for the keyword '{keyword}' cannot be null.");

            AddToken(keyword, value);
        }
    }
}
