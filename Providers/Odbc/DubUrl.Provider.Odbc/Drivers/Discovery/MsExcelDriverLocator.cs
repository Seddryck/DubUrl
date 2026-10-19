using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Provider.Excel.Metadata;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Provider.Odbc.Drivers.Discovery;

[Driver<MsExcelDriverRegex, OdbcDbqMapper, MsExcelDatabase>()]
public class MsExcelDriverLocator : BaseDriverLocator
{
    internal class MsExcelDriverRegex : BaseDriverRegex
    {
        public MsExcelDriverRegex()
            : base(
            [
                new WordMatch("Microsoft Excel Driver"),
                new SpaceMatch(),
                new LiteralMatch("(*.xls, *.xlsx, *.xlsm, *.xlsb)"),
            ])
        { }
    }
    private List<string> Candidates { get; } = [];

    public MsExcelDriverLocator()
        : base(GetRegexPattern<MsExcelDriverLocator>()) { }
    internal MsExcelDriverLocator(DriverLister driverLister)
        : base(GetRegexPattern<MsExcelDriverLocator>(), driverLister) { }

    protected override void AddCandidate(string driver, string[] matches)
        => Candidates.Add(driver);

    protected override List<string> RankCandidates()
        => Candidates;
}
