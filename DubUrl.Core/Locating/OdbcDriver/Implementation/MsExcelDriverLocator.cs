using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [Driver<MsExcelDriverRegex, OdbcDbqMapper, MsExcelDatabase>()]
    internal class MsExcelDriverLocator : BaseDriverLocator
    {
        internal class MsExcelDriverRegex : BaseDriverRegex
        {
            public MsExcelDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("Microsoft Excel Driver"),
                    new SpaceMatch(),
                    new LiteralMatch("(*.xls, *.xlsx, *.xlsm, *.xlsb)"),
                })
            { }
        }
        private List<string> Candidates { get; } = new();

        public MsExcelDriverLocator()
            : base(GetRegexPattern<MsExcelDriverLocator>()) { }
        internal MsExcelDriverLocator(DriverLister driverLister)
            : base(GetRegexPattern<MsExcelDriverLocator>(), driverLister) { }


        protected override void AddCandidate(string driver, string[] matches)
            => Candidates.Add(driver);

        protected override List<string> RankCandidates()
            => Candidates;
    }
}
