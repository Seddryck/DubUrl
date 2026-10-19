using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver;

[Driver<MsAccessDriverRegex, OdbcDbqMapper, MsAccessDatabase>()]
public class MsAccessDriverLocator : BaseDriverLocator
{
    internal class MsAccessDriverRegex : BaseDriverRegex
    {
        public MsAccessDriverRegex()
            : base(
            [
                new WordMatch("Microsoft Access Driver"),
                new SpaceMatch(),
                new LiteralMatch("(*.mdb, *.accdb)"),
            ])
        { }
    }
    private List<string> Candidates { get; } = [];

    public MsAccessDriverLocator()
        : base(GetRegexPattern<MsAccessDriverLocator>()) { }
    internal MsAccessDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<MsAccessDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
        => Candidates.Add(driver);

    protected override List<string> RankCandidates()
        => Candidates;
}
