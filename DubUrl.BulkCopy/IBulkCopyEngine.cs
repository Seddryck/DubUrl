using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy;
public interface IBulkCopyEngine : IDisposable
{
    void Write(string tableName, IDataReader dataReader);
}
