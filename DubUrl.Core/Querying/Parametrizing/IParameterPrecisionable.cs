using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

public interface IParameterPrecisionable
{
    public byte Precision { get; }
    public byte Scale { get; }
}
