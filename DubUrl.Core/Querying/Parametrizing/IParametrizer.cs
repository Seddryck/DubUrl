using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

public interface IParametrizer
{
    IDbDataParameter CreateParameter(IDbCommand command, DubUrlParameter parameter);

    /// <summary>Creates the placeholder for a template occurrence (one-based position).</summary>
    string GetPlaceholder(int position) => throw new NotSupportedException("This parametrizer does not support bound templates.");

    IDbDataParameter CreateTemplateParameter(IDbCommand command, DubUrlParameter parameter) => CreateParameter(command, parameter);

    /// <summary>Configures binding for template-generated commands only.</summary>
    void ConfigureTemplateCommand(IDbCommand command) { }
}
