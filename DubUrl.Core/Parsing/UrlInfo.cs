using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Parsing
{
    public record class UrlInfo
    (
        string Host = "",
        int Port = 0,
        string Username = "",
        string Password = ""
    )
    {
        public string[] Schemes { get; init; } = Array.Empty<string>();
        public string[] Segments { get; init; } = Array.Empty<string>();
        public IDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
    }
}
