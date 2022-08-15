using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver.Implementation
{
    [Driver<MssqlDriverRegex, MssqlMapper>(
        new[] { typeof(VersionOption) }
    )]
    internal class MssqlDriverLocator : BaseDriverLocator
    {
        internal class MssqlDriverRegex : CompositeRegex, IRegexDriver
        {
            public MssqlDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("ODBC Driver"),
                    new SpaceMatch(),
                    new VersionCapture(),
                    new SpaceMatch(),
                    new WordMatch("for SQL Server"),
                })
            { }
        }

        private readonly Dictionary<string, int> Candidates = new();
        public MssqlDriverLocator()
            : base(GetNamePattern<MssqlDriverLocator>()) { }
        internal MssqlDriverLocator(DriverLister driverLister)
            : base(GetNamePattern<MssqlDriverLocator>(), driverLister) { }

        protected override void AddCandidate(string driver, string[] matches)
            => Candidates.Add(driver, int.Parse(matches[GetOptionPosition<MssqlDriverLocator>(typeof(VersionOption))]));

        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }
}