using DubUrl.Querying.Parametrizing;

namespace DubUrl.Provider.PostgreSql.Mapping;

public class NpgsqlParametrizer : PositionalParametrizer
{
    public override string GetPlaceholder(int position) => "$" + position.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
