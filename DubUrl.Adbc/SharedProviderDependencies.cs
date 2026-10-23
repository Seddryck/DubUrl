using DubUrl.Mapping;

// Native sessions expose these types across the installed-provider boundary.
[assembly: SharedProviderDependency(typeof(DubUrl.Adbc.AdbcProviderFactory))]
[assembly: SharedProviderDependency(typeof(Apache.Arrow.Adbc.AdbcConnection))]
[assembly: SharedProviderDependency(typeof(Apache.Arrow.RecordBatch))]
