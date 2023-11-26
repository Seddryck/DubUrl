using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying;

public record struct ParameterInfo(string Name, string DbTypeText, string DirectionText) { }
