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
    [Driver<PostgresqlDriverRegex, PgsqlMapper>()]
    internal class PostgresqlDriverLocator : BaseDriverLocator
    {
        internal class PostgresqlDriverRegex : CompositeRegex, IDriverRegex
        {
            public PostgresqlDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("PostgreSQL"),
                    new SpaceMatch(),
                    new AnyOfCapture(new[] { "ANSI", "Unicode" }),
                    new OptionalCapture("(x64)"),
                })
            { }
            public Type[] Options { get => new[] { typeof(EncodingOption), typeof(ArchitectureOption) }; }
        }

        private record struct CandidateInfo(string Driver, EncodingOption Encoding, ArchitectureOption Architecture);

        private readonly List<CandidateInfo> Candidates = new();
        internal EncodingOption Encoding { get; }
        internal ArchitectureOption Architecture { get; }

        public PostgresqlDriverLocator()
            : this(EncodingOption.Unspecified, ArchitectureOption.Unspecified) { }
        public PostgresqlDriverLocator(EncodingOption encoding, ArchitectureOption architecture)
            : base(GetNamePattern<PostgresqlDriverLocator>()) => (Encoding, Architecture) = (encoding, architecture);
        internal PostgresqlDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified, ArchitectureOption architecture = ArchitectureOption.Unspecified)
            : base(GetNamePattern<PostgresqlDriverLocator>(), driverLister) => (Encoding, Architecture) = (encoding, architecture);

        protected override void AddCandidate(string driver, string[] matches)
        {
            var encoding = (EncodingOption)Enum.Parse
            (
                typeof(EncodingOption)
                , matches[GetOptionPosition<PostgresqlDriverLocator>(typeof(EncodingOption))]
            );
            var architecture = (ArchitectureOption)Enum.Parse
            (
                typeof(ArchitectureOption)
                , matches.Length > 1 && !string.IsNullOrEmpty(matches[GetOptionPosition<PostgresqlDriverLocator>(typeof(ArchitectureOption))])
                    ? matches[GetOptionPosition<PostgresqlDriverLocator>(typeof(ArchitectureOption))].Replace("(", "").Replace(")", "")
                    : "x86"
            );

            if (Encoding != EncodingOption.Unspecified && encoding != Encoding)
                return;
            if (Architecture != ArchitectureOption.Unspecified && architecture != Architecture)
                return;

            Candidates.Add(new CandidateInfo(driver, encoding, architecture));
        }

        protected virtual ArchitectureOption GetRunningArchitecture()
            => ArchitectureOption.x64;

        protected override List<string> RankCandidates()
            => Candidates
                .OrderByDescending(x => x.Encoding)
                .OrderByDescending(x => x.Architecture == GetRunningArchitecture())
                .Select(x => x.Driver)
                .ToList();
    }
}
