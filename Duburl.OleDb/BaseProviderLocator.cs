﻿using DubUrl.Locating;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.OleDb
{
    internal abstract class BaseProviderLocator : IProviderLocator
    {
        
        private string RegexPattern { get; }
        private Func<ProviderInfo, string> NamePointer { get; }
        private ProviderLister Lister { get; }
        public BaseTokenMapper[] AdditionalMappers { get; } = new[] { new BaseMapper.OptionsMapper() };

        public BaseProviderLocator(string regexPattern)
            : this(regexPattern, new Func<ProviderInfo, string>(x => x.NickName)) { }

        public BaseProviderLocator(string regexPattern, BaseTokenMapper[] additionalMappers)
            : this(regexPattern, new Func<ProviderInfo, string>(x => x.NickName)) { AdditionalMappers = additionalMappers; }

        public BaseProviderLocator(string regexPattern, Func<ProviderInfo, string> namePointer)
            : this(regexPattern, namePointer, new ProviderLister()) { }

        public BaseProviderLocator(string regexPattern, ProviderLister lister)
            => (RegexPattern, NamePointer, Lister) = (regexPattern, new(x => x.NickName), lister);

        public BaseProviderLocator(string regexPattern, Func<ProviderInfo, string> namePointer, ProviderLister lister)
            => (RegexPattern, NamePointer, Lister) = (regexPattern, namePointer, lister);

        public virtual string Locate()
        {
            var allProviders = Lister.List();
            foreach (var provider in allProviders)
            {
                var matches = Regex.Matches(NamePointer.Invoke(provider), RegexPattern);
                if (matches.Any())
                    AddCandidate(provider.NickName, matches[0].Groups.Values.Select(x => x.Value).Skip(1).ToArray());
            }
            var bestCandidates = RankCandidates();
            return bestCandidates.Any() ? bestCandidates.First() : string.Empty;
        }
        protected internal static string GetRegexPattern<T>()
        =>
            (typeof(T).GetCustomAttributes(typeof(BaseLocatorAttribute), false).FirstOrDefault() as BaseLocatorAttribute)
            ?.RegexPattern
            ?? throw new ArgumentOutOfRangeException();

        protected abstract void AddCandidate(string driver, string[] matches);
        protected abstract List<string> RankCandidates();
    }
}