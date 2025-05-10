using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;
public interface ISchemeRegistry
{
    IMapper GetMapper(string alias);
    IMapper GetMapper(string[] aliases);
    DbProviderFactory GetProviderFactory(string[] aliases);
    bool CanHandle(string scheme);
}
