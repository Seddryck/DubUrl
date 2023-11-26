using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Extensions;

public interface IDubUrlProviderFactoryWrapper
{
    DbConnection CreateConnection();
    DbConnectionStringBuilder CreateConnectionStringBuilder();
}
