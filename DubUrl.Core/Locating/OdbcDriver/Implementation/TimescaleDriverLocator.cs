using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver.Implementation
{
    [Driver<PostgresqlDriverRegex>(
        "Timescale"
        , new[] { "ts", "Timescale" }
        , new[] { typeof(EncodingOption), typeof(ArchitectureOption) }
        , 6
    )]
    internal class TimescaleDriverLocator : PostgresqlDriverLocator
    {
        public TimescaleDriverLocator()
            : this(EncodingOption.Unspecified, ArchitectureOption.Unspecified) { }
        public TimescaleDriverLocator(EncodingOption encoding, ArchitectureOption architecture)
            : base(encoding, architecture) { }
        internal TimescaleDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified, ArchitectureOption architecture = ArchitectureOption.Unspecified)
            : base(driverLister, encoding, architecture) { }
    }
}
