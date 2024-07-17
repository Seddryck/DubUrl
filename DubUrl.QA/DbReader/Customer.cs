using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA.DbReader;

internal record Customer(
    int CustomerId,
    string FullName,
    DateTime BirthDate
)
{ }
