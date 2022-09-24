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
    [Driver<TextDriverRegex, OdbcDbqMapper, TextDatabase>()]
    internal class TextDriverLocator : BaseDriverLocator
    {
        internal class TextDriverRegex : BaseDriverRegex
        {
            public TextDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("Microsoft Access Driver"),
                    new SpaceMatch(),
                    new LiteralMatch("(*.txt, *.csv)"),
                })
            { }
        }
        private readonly List<string> Candidates = new();

        public TextDriverLocator()
            : base(GetRegexPattern<TextDriverLocator>()) { }
        internal TextDriverLocator(DriverLister driverLister)
            : base(GetRegexPattern<TextDriverLocator>(), driverLister) { }


        protected override void AddCandidate(string driver, string[] matches)
            => Candidates.Add(driver);

        protected override List<string> RankCandidates()
            => Candidates;
    }
}