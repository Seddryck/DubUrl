﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [Driver(
        "PostgreSQL"
        , new[] { "pg", "pgsql", "postgres", "postgresql" }
        , "^\\bPostgreSQL \\b(\\bANSI\\b|\\bUnicode\\b)\\(?(\\bx64\\b)?\\)?$"
        , new[] { typeof(EncodingOption), typeof(ArchitectureOption) }
        , 1
    )]
    internal class PostgresqlDriverLocator : BaseDriverLocator
    {
        private readonly List<string> Candidates = new();
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
                    ? matches[GetOptionPosition<PostgresqlDriverLocator>(typeof(ArchitectureOption))] 
                    : "x86"
            );

            if (Encoding != EncodingOption.Unspecified && encoding != Encoding)
                return;
            if (Architecture != ArchitectureOption.Unspecified && architecture != Architecture)
                return;

            Candidates.Add(driver);
        }

        protected override List<string> RankCandidates()
            => Candidates.ToList();
    }
}
