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

[Driver<DuckdbDriverRegex, OdbcMapper, DuckdbDatabase>()]
internal class DuckdbDriverLocator : BaseDriverLocator
{
    internal class DuckdbDriverRegex : BaseDriverRegex
    {
        public DuckdbDriverRegex()
            : base(new BaseRegex[]
            {
                new WordMatch("DuckDB"),
                new SpaceMatch(),
                new WordMatch("Driver"),
            })
        { }
    }
    private List<string> Candidates { get; } = new();
    public DuckdbDriverLocator()
        : base(GetRegexPattern<DuckdbDriverLocator>()) { }
    internal DuckdbDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<DuckdbDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
        => Candidates.Add(driver);

    protected override List<string> RankCandidates()
        => Candidates.ToList();
}
