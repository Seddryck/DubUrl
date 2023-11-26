using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb;

public interface IProviderRegex
{
    string ToString();
    Type[] Options { get; }
}
