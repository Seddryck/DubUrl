using System.Data;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Provider.PostgreSql.Mapping;

/// <summary>Preserves named parameters for handwritten CrateDB SQL while generating native positional templates.</summary>
public class NpgsqlNamedParametrizer : NamedParametrizer
{
    public override string GetPlaceholder(int position) => "$" + position.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public override IDbDataParameter CreateTemplateParameter(IDbCommand command, DubUrlParameter parameter)
        => new PositionalParametrizer().CreateParameter(command, parameter);
}
