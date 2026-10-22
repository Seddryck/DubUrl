using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Provider.MySql.Metadata;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Locating.Options;
using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Provider.Odbc.Drivers.Discovery;

[Driver<MariaDbDriverRegex, OdbcMapper, MariaDbDatabase>()]
public class MariaDbDriverLocator : BaseDriverLocator
{
    internal class MariaDbDriverRegex : BaseDriverRegex
    {
        public MariaDbDriverRegex()
            : base(
            [
                new WordMatch("MariaDB ODBC"),
                new SpaceMatch(),
                new VersionCapture<VersionOption>(),
                new SpaceMatch(),
                new WordMatch("Driver"),
            ])
        { }
    }

    private Dictionary<string, decimal> Candidates { get; } = [];
    internal EncodingOption Encoding { get; }

    public MariaDbDriverLocator()
        : base(GetRegexPattern<MariaDbDriverLocator>()) { }
    internal MariaDbDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<MariaDbDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
    {
        var version = decimal.Parse
        (
            matches[GetOptionPosition<MariaDbDriverLocator>(typeof(VersionOption))]
            , System.Globalization.NumberStyles.AllowDecimalPoint
            , System.Globalization.CultureInfo.InvariantCulture.NumberFormat
        );
        Candidates.Add(driver, version);
    }

    protected override List<string> RankCandidates()
        => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
}
