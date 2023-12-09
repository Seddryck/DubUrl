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

[Driver<TrinoDriverRegex, TrinoOdbcMapper, TrinoDatabase>()]
public class TrinoDriverLocator : BaseDriverLocator
{
    internal class TrinoDriverRegex : BaseDriverRegex
    {
        public TrinoDriverRegex()
            : base(new BaseRegex[]
            {
                new AnyOfCapture(new [] { "Simba" }),
                new SpaceMatch(),
                new WordMatch("Trino ODBC Driver"),
            })
        { }
    }

    private List<string> Candidates { get; } = new();
    public TrinoDriverLocator()
        : base(GetRegexPattern<TrinoDriverLocator>()) { }
    internal TrinoDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<TrinoDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
        => Candidates.Add(driver);

    protected override List<string> RankCandidates()
        => Candidates.OrderByDescending(x => x).ToList();
}
