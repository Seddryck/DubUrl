using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb
{
    public interface IProviderLocator
    {
        BaseTokenMapper[] AdditionalMappers { get; }
        string Locate();
    }
}
