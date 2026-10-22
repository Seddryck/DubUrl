using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Provider.DuckDb.Metadata;
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

[Driver<DuckdbDriverRegex, DuckdbOdbcMapper, DuckdbDatabase>()]
public class DuckdbDriverLocator : BaseDriverLocator
{
    internal class DuckdbDriverRegex : BaseDriverRegex
    {
        public DuckdbDriverRegex()
            : base(
            [
                new WordMatch("DuckDB"),
                new SpaceMatch(),
                new WordMatch("Driver"),
            ])
        { }
    }
    private List<string> Candidates { get; } = [];
    public DuckdbDriverLocator()
        : base(GetRegexPattern<DuckdbDriverLocator>()) { }
    internal DuckdbDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<DuckdbDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
        => Candidates.Add(driver);

    protected override List<string> RankCandidates()
        => [.. Candidates];
}
