using DubUrl.Mapping.Implementation;
using DubUrl.Mapping.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DubUrl.Locating.Options;

namespace DubUrl.Locating.OdbcDriver.Implementation;

[Driver<PostgresqlDriverRegex, QuestDbOdbcMapper, QuestDbDatabase>()]
internal class QuestDbDriverLocator : PostgresqlDriverLocator
{
    public QuestDbDriverLocator()
        : this(EncodingOption.Unspecified, ArchitectureOption.Unspecified) { }
    public QuestDbDriverLocator(EncodingOption encoding, ArchitectureOption architecture)
        : base(encoding, architecture) { }
    internal QuestDbDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified, ArchitectureOption architecture = ArchitectureOption.Unspecified)
        : base(driverLister, encoding, architecture) { }
}
