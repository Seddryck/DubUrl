using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy;
public class BulkCopyEngineFactory
{
    private readonly IBulkCopyDependencyResolver dependencies;

    public BulkCopyEngineFactory() : this(BulkCopyDependencyResolver.Default) { }

    public BulkCopyEngineFactory(IBulkCopyDependencyResolver dependencies)
        => this.dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));

    public virtual IBulkCopyEngine Create(ConnectionUrl connectionUrl)
    {
        ArgumentNullException.ThrowIfNull(connectionUrl, nameof(connectionUrl));
        return connectionUrl.BulkCopyProvider?.Create(connectionUrl, dependencies)
            ?? throw new NotSupportedException("Bulk copy is not supported by the selected provider/connector registration.");
    }
}
