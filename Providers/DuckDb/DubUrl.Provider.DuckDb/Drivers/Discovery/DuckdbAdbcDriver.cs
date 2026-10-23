using DubUrl.Provider.DuckDb.Metadata;
using DubUrl.Provider.DuckDb.Mapping;
using DubUrl.Locating.AdbcDriver;

namespace DubUrl.Provider.DuckDb.Drivers.Discovery;

[AdbcDriver<DuckdbAdbcMapper, DuckdbDatabase>]
public class DuckdbAdbcDriver { }
