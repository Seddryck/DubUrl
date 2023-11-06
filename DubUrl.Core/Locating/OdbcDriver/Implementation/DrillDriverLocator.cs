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

namespace DubUrl.Locating.OdbcDriver.Implementation
{
    [Driver<DrillDriverRegex, DrillOdbcMapper, DrillDatabase>()]
    internal class DrillDriverLocator : BaseDriverLocator
    {
        internal class DrillDriverRegex : BaseDriverRegex
        {
            public DrillDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("MapR Drill ODBC Driver"),
                })
            { }
        }

        private List<string> Candidates { get; } = new();
        internal EncodingOption Encoding { get; }

        public DrillDriverLocator()
            : base(GetRegexPattern<DrillDriverLocator>()) { }
        internal DrillDriverLocator(DriverLister driverLister)
            : base(GetRegexPattern<DrillDriverLocator>(), driverLister) { }

        protected override void AddCandidate(string driver, string[] matches)
            => Candidates.Add(driver);

        protected override List<string> RankCandidates()
            => Candidates.ToList();

        
    }
}
