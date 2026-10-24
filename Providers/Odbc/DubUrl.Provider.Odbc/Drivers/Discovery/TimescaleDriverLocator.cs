using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Provider.PostgreSql.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DubUrl.Locating.Options;

namespace DubUrl.Provider.Odbc.Drivers.Discovery;

[Driver<PostgresqlDriverRegex, PostgresqlOdbcMapper, TimescaleDatabase>()]
public class TimescaleDriverLocator : PostgresqlDriverLocator
{
    public TimescaleDriverLocator()
        : this(EncodingOption.Unspecified, ArchitectureOption.Unspecified) { }
    public TimescaleDriverLocator(EncodingOption encoding, ArchitectureOption architecture)
        : base(encoding, architecture) { }
    internal TimescaleDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified, ArchitectureOption architecture = ArchitectureOption.Unspecified)
        : base(driverLister, encoding, architecture) { }
}
