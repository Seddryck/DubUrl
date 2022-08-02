using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [Driver(
        "Microsoft Excel"
        , new[] { "xls", "xlsx", "xlsm", "xlsb" }
        , "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)"
        , new Type[] { }
        , 0
    )]
    internal class MsExcelDriverLocator : BaseDriverLocator
    {
        private readonly List<string> Candidates = new();

        public MsExcelDriverLocator()
            : base(GetNamePattern<MsExcelDriverLocator>()) { }
        internal MsExcelDriverLocator(DriverLister driverLister)
            : base(GetNamePattern<MsExcelDriverLocator>(), driverLister) { }

        public override string Locate()
        {
            var allDrivers = Lister.List();
            foreach (var driver in allDrivers)
            {
                if (StringComparer.InvariantCultureIgnoreCase.Compare(RegexPattern, driver)==0)
                    AddCandidate(driver, new[] { driver });
            }
            var bestCandidates = RankCandidates();
            return bestCandidates.Any() ? bestCandidates.First() : string.Empty;
        }

        protected override void AddCandidate(string driver, string[] matches)
            => Candidates.Add(driver);

        protected override List<string> RankCandidates()
            => Candidates;
    }
}