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

namespace DubUrl.Locating.OdbcDriver.Implementation;

[Driver<PostgresqlDriverRegex, OdbcMapper, PostgresqlDatabase>()]
public class PostgresqlDriverLocator : BaseDriverLocator
{
    internal class PostgresqlDriverRegex : BaseDriverRegex
    {
        public PostgresqlDriverRegex()
            : base(
            [
                new WordMatch("PostgreSQL"),
                new SpaceMatch(),
                new AnyOfCapture<EncodingOption>(["ANSI", "Unicode"]),
                new OptionalCapture<ArchitectureOption>("(x64)"),
            ])
        { }
    }

    private record struct CandidateInfo(string Driver, EncodingOption Encoding, ArchitectureOption Architecture);

    private List<CandidateInfo> Candidates { get; } = [];
    internal EncodingOption Encoding { get; }
    internal ArchitectureOption Architecture { get; }

    public PostgresqlDriverLocator()
        : this(EncodingOption.Unspecified, ArchitectureOption.Unspecified) { }
    public PostgresqlDriverLocator(EncodingOption encoding, ArchitectureOption architecture)
        : base(GetRegexPattern<PostgresqlDriverLocator>()) => (Encoding, Architecture) = (encoding, architecture);
    internal PostgresqlDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified, ArchitectureOption architecture = ArchitectureOption.Unspecified)
        : base(GetRegexPattern<PostgresqlDriverLocator>(), driverLister) => (Encoding, Architecture) = (encoding, architecture);

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
