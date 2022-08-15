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
    [Driver<MySqlConnectorDriverRegex, MySqlConnectorMapper>()]
    internal class MySqlConnectorDriverLocator : BaseDriverLocator
    {
        internal class MySqlConnectorDriverRegex : BaseDriverRegex
        {
            public MySqlConnectorDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("MySQL ODBC"),
                    new SpaceMatch(),
                    new VersionCapture<VersionOption>(),
                    new SpaceMatch(),
                    new AnyOfCapture<EncodingOption>(new[] { "ANSI", "Unicode" }),
                    new SpaceMatch(),
                    new WordMatch("Driver"),
                })
            { }
        }

        private readonly Dictionary<string, decimal> Candidates = new();
        internal EncodingOption Encoding { get; }

        public MySqlConnectorDriverLocator()
            : this(EncodingOption.Unspecified) { }
        public MySqlConnectorDriverLocator(EncodingOption encoding)
            : base(GetNamePattern<MySqlConnectorDriverLocator>()) => Encoding = encoding;
        internal MySqlConnectorDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified)
            : base(GetNamePattern<MySqlConnectorDriverLocator>(), driverLister) => Encoding = encoding;

        protected override void AddCandidate(string driver, string[] matches)
        {
            var version = decimal.Parse
            (
                matches[GetOptionPosition<MySqlConnectorDriverLocator>(typeof(VersionOption))]
                , System.Globalization.NumberStyles.AllowDecimalPoint
                , System.Globalization.CultureInfo.InvariantCulture.NumberFormat
            );
            var encoding = (EncodingOption)Enum.Parse
            (
                typeof(EncodingOption)
                , matches[GetOptionPosition<MySqlConnectorDriverLocator>(typeof(EncodingOption))]
            );

            if (Encoding != EncodingOption.Unspecified && encoding != Encoding)
                return;

            Candidates.Add(driver, version);
        }

        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }
}