using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.QA.Dapper;

internal class DapperConfiguration : IDapperConfiguration
{
    private readonly string url;
    public DapperConfiguration(string url)
        => this.url = url;

    public string GetConnectionString() => url;
}
