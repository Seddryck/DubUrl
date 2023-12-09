using DubUrl.Locating.Options;
using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver.Implementation;

[Driver<MySqlConnectorDriverRegex, OdbcMapper, MySqlDatabase>()]
public class MySqlConnectorDriverLocator : BaseDriverLocator
{
    internal class MySqlConnectorDriverRegex : BaseDriverRegex
    {
        public MySqlConnectorDriverRegex()
            : base(new BaseRegex[]
            {
                new WordMatch("MySQL ODBC"),
                new SpaceMatch(),
                new VersionCapture<VersionOption>(),
                new SpaceMatch(),
                new AnyOfCapture<EncodingOption>(new[] { "ANSI", "Unicode" }),
                new SpaceMatch(),
                new WordMatch("Driver"),
            })
        { }
    }

    private Dictionary<string, decimal> Candidates { get; } = new();
    internal EncodingOption Encoding { get; }

    public MySqlConnectorDriverLocator()
        : this(EncodingOption.Unspecified) { }
    public MySqlConnectorDriverLocator(EncodingOption encoding)
        : base(GetRegexPattern<MySqlConnectorDriverLocator>()) => Encoding = encoding;
    internal MySqlConnectorDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified)
        : base(GetRegexPattern<MySqlConnectorDriverLocator>(), driverLister) => Encoding = encoding;

    protected override void AddCandidate(string driver, string[] matches)
    {
        var version = decimal.Parse
        (
            matches[GetOptionPosition<MySqlConnectorDriverLocator>(typeof(VersionOption))]
            , System.Globalization.NumberStyles.AllowDecimalPoint
            , System.Globalization.CultureInfo.InvariantCulture.NumberFormat
        );
        var encoding = (EncodingOption)Enum.Parse
        (
            typeof(EncodingOption)
            , matches[GetOptionPosition<MySqlConnectorDriverLocator>(typeof(EncodingOption))]
        );

        if (Encoding != EncodingOption.Unspecified && encoding != Encoding)
            return;

        Candidates.Add(driver, version);
    }

    protected override List<string> RankCandidates()
        => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
}
