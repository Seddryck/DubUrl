using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Provider.Drill.Metadata;
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

[Driver<DrillDriverRegex, DrillOdbcMapper, DrillDatabase>()]
public class DrillDriverLocator : BaseDriverLocator
{
    internal class DrillDriverRegex : BaseDriverRegex
    {
        public DrillDriverRegex()
            : base(
            [
                new WordMatch("MapR Drill ODBC Driver"),
            ])
        { }
    }

    private List<string> Candidates { get; } = [];
    internal EncodingOption Encoding { get; }

    public DrillDriverLocator()
        : base(GetRegexPattern<DrillDriverLocator>()) { }
    internal DrillDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<DrillDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
        => Candidates.Add(driver);

    protected override List<string> RankCandidates()
        => [.. Candidates];
}
