﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.DriverLocating
{
    internal abstract class BaseDriverLocator : IDriverLocator
    {
        private string RegexPattern { get; }
        private DriverLister Lister { get; }

        public BaseDriverLocator(string regexPattern)
            : this(regexPattern, new DriverLister()) { }

        public BaseDriverLocator(string regexPattern, DriverLister lister)
            => (RegexPattern, Lister) = (regexPattern, lister);

        public virtual string Locate()
        {
            var allDrivers = Lister.List();
            foreach (var driver in allDrivers)
            {
                var matches = Regex.Matches(driver, RegexPattern);
                if (matches.Any())
                    AddCandidate(driver, matches);
            }
            var bestCandidates = RankCandidates();
            return bestCandidates.Any() ? bestCandidates.First() : string.Empty;
        }

        protected abstract void AddCandidate(string driver, MatchCollection matches);
        protected abstract List<string> RankCandidates();
    }
}
