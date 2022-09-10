using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA
{
    internal class SelectFirstCustomer : EmbeddedSqlFileQuery
    {
        public SelectFirstCustomer()
            : base($"{typeof(SelectFirstCustomer).Assembly.GetName().Name}.{nameof(SelectFirstCustomer)}")
        { }
    }
}
