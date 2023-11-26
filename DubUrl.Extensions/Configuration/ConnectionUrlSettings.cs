using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace DubUrl.Extensions.Configuration;

public record struct ConnectionUrlSettings
(
    string Scheme,
    string Host,
    int? Port,
    string? Username,
    string? Password,
    string[]? Segments,
    string[]? Keys,
    string[]? Values
)
{
    public override string ToString()
    {
        IDictionary<string, string> parameters;
        if (Keys is not null || Values is not null)
        {
            if (!(Keys is not null && Values is not null))
                throw new ArgumentException("Cannot have keys without values or the opposite");
            if (!(Keys.Length == Values.Length))
                throw new ArgumentException("Keys and Values should have the same length");
            var values = Values;
            parameters = new Dictionary<string, string>(
                Keys.Select((x, i) => new KeyValuePair<string, string>(x, values[i]))
            );
        }
        else
        {
            parameters = new Dictionary<string, string>();
        }

        var encoder = UrlEncoder.Default;
        var builder = new UriBuilder
        {
            Scheme = Scheme,
            Host = encoder.Encode(Host)
        };
        if (Port.HasValue)
            builder.Port = Port.Value;
        if (!string.IsNullOrEmpty(Username))
            builder.UserName = encoder.Encode(Username);
        if (!string.IsNullOrEmpty(Password))
            builder.Password = encoder.Encode(Password);
        if (Segments is not null && Segments.Any())
            builder.Path = string.Join('/', Segments.Select(encoder.Encode));
        if (parameters.Any())
            builder.Query = string.Join("&", parameters.Select(x => $"{encoder.Encode(x.Key)}={encoder.Encode(x.Value)}"));
        return builder.ToString();
    }

}
