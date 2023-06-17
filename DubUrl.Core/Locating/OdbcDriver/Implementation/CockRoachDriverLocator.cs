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
    [Driver<PostgresqlDriverRegex, CockRoachOdbcMapper, CockRoachDatabase>()]
    internal class CockRoachDriverLocator : PostgresqlDriverLocator
    {
        public CockRoachDriverLocator()
            : this(EncodingOption.Unspecified, ArchitectureOption.Unspecified) { }
        public CockRoachDriverLocator(EncodingOption encoding, ArchitectureOption architecture)
            : base(encoding, architecture) { }
        internal CockRoachDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified, ArchitectureOption architecture = ArchitectureOption.Unspecified)
            : base(driverLister, encoding, architecture) { }
    }
}
