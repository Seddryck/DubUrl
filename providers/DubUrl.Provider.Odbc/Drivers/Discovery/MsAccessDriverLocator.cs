using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Provider.Access.Metadata;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Provider.Odbc.Drivers.Discovery;

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
