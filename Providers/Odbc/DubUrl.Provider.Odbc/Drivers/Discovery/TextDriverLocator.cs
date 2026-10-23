using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Provider.Text.Metadata;
using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Provider.Odbc.Drivers.Discovery;

[Driver<TextDriverRegex, OdbcDbqMapper, TextDatabase>()]
public class TextDriverLocator : BaseDriverLocator
{
    internal class TextDriverRegex : BaseDriverRegex
    {
        public TextDriverRegex()
            : base(
            [
                new WordMatch("Microsoft Access Text Driver"),
                new SpaceMatch(),
                new LiteralMatch("(*.txt, *.csv)"),
            ])
        { }
    }
    private List<string> Candidates { get; } = [];

    public TextDriverLocator()
        : base(GetRegexPattern<TextDriverLocator>()) { }
    internal TextDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<TextDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
        => Candidates.Add(driver);

    protected override List<string> RankCandidates()
        => Candidates;
}
