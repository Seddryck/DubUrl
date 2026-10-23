using System.Data;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Provider.Oracle.Mapping;

public class OracleParametrizer : NamedParametrizer
{
    public override string GetPlaceholder(int position) => ":p" + position.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public override void ConfigureTemplateCommand(IDbCommand command)
    {
        // Keep the dialect assembly independent of the optional ODP.NET runtime.
        var property = command.GetType().GetProperty("BindByName");
        if (property?.PropertyType != typeof(bool) || !property.CanWrite)
            throw new InvalidOperationException("Oracle template commands require a writable BindByName property.");
        property.SetValue(command, true);
    }
}
