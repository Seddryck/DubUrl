using DubUrl.Mapping.Implementation;
using DubUrl.Mapping.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DubUrl.Locating.Options;

namespace DubUrl.Locating.OdbcDriver.Implementation
{
    [Driver<PostgresqlDriverRegex, OdbcMapper, TimescaleDatabase>()]
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
