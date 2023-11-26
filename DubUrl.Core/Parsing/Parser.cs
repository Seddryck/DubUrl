using DubUrl.Mapping;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DubUrl.Parsing;

internal class Parser : IParser
{
    public virtual UrlInfo Parse(string url)
    {
        var uri = new Uri(url);
        var urlInfo = new UrlInfo();

        urlInfo = urlInfo with { Schemes = uri.Scheme.Split(new[] { '+', ':' }) };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            urlInfo = urlInfo with
            {
                Username = uri.UserInfo.Split(':')[0],
                Password = uri.UserInfo.Contains(':') ? uri.UserInfo.Split(':')[1] : string.Empty,
            };
        }
        urlInfo = urlInfo with { Host = uri.Host };

        if (!uri.IsDefaultPort)
            urlInfo = urlInfo with { Port = uri.Port };

        string[] segments;
        if (uri.Segments.Length >= 1)
        {
            segments = uri.Segments.Skip(1).ToArray();
            for (int i = 0; i <= segments.Length - 1; i++)
                segments[i] = segments[i].TrimEnd('/');
        }
        else
            segments = Array.Empty<string>();
        
        urlInfo = urlInfo with { Segments = segments };

        if (!string.IsNullOrEmpty(uri.Query))
        {
            var nameValues = HttpUtility.ParseQueryString(uri.Query);
            var options = new Dictionary<string, string>();
            foreach (var key in nameValues.AllKeys)
            {
                if (key != null)
                {
                    if (options.ContainsKey(key))
                        throw new InvalidConnectionUrlException("The query string cannot contains twice the same keyword.");
                    var value = nameValues[key];
                    if (value != null)
                        options.Add(key, value);
                }
            }
            urlInfo = urlInfo with { Options = options };
        }
        return urlInfo;
    }
}
