using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    internal abstract class BaseDriverLocator : IDriverLocator
    {
        protected string RegexPattern { get; }
        protected DriverLister Lister { get; }

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
                    AddCandidate(driver, matches[0].Groups.Values.Select(x => x.Value).Skip(1).ToArray());
            }
            var bestCandidates = RankCandidates();
            return bestCandidates.Any() ? bestCandidates.First() : string.Empty;
        }

        protected abstract void AddCandidate(string driver, string[] match);
        protected abstract List<string> RankCandidates();

        protected internal static string GetRegexPattern<T>()
        =>
            (typeof(T).GetCustomAttributes(typeof(LocatorAttribute), false).FirstOrDefault() as LocatorAttribute)
            ?.RegexPattern
            ?? throw new ArgumentOutOfRangeException();

        protected internal static int GetOptionPosition<T>(Type optionType)
        =>
            (typeof(T).GetCustomAttributes(typeof(LocatorAttribute), false).FirstOrDefault() as LocatorAttribute)
            ?.Options.ToList().IndexOf(optionType)
            ?? throw new ArgumentOutOfRangeException(optionType.Name);
    }
}
