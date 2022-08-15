using DubUrl.Locating.RegexUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [Driver<MsExcelDriverRegex>(
        "Microsoft Excel"
        , new[] { "xls", "xlsx", "xlsm", "xlsb" }
        , new Type[] { }
        , 2
    )]
    internal class MsExcelDriverLocator : BaseDriverLocator
    {
        internal class MsExcelDriverRegex : CompositeRegex, IDriverRegex
        {
            public MsExcelDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("Microsoft Excel Driver"),
                    new SpaceMatch(),
                    new LiteralMatch("(*.xls, *.xlsx, *.xlsm, *.xlsb)"),
                })
            { }
            public Type[] Options { get => Array.Empty<Type>(); }
        }
        private readonly List<string> Candidates = new();

        public MsExcelDriverLocator()
            : base(GetNamePattern<MsExcelDriverLocator>()) { }
        internal MsExcelDriverLocator(DriverLister driverLister)
            : base(GetNamePattern<MsExcelDriverLocator>(), driverLister) { }


        protected override void AddCandidate(string driver, string[] matches)
            => Candidates.Add(driver);

        protected override List<string> RankCandidates()
            => Candidates;
    }
}