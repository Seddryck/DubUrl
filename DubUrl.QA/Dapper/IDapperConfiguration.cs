using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA.Dapper;

internal interface IDapperConfiguration
{
    string GetConnectionString();
}
