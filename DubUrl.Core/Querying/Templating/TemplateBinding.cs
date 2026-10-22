using System.Data;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Querying.Templating;

/// <summary>An immutable snapshot of one evaluated parameter occurrence.</summary>
public sealed class TemplateBinding : DubUrlParameter, IParameterSizable, IParameterPrecisionable
{
    public DbType DbType { get; }
    public int Size { get; }
    public byte Precision { get; }
    public byte Scale { get; }

    internal TemplateBinding(string name, object? input) : base(name)
    {
        if (input is TemplateParameter typed)
            (Value, DbType, Size, Precision, Scale) = (typed.Value, typed.DbType, typed.Size ?? 0, typed.Precision ?? 0, typed.Scale ?? 0);
        else
        {
            var parameter = input as DubUrlParameter ?? (input is null || input == DBNull.Value
                ? new DubUrlParameterObject(name, null)
                : new DubUrlParameterFactory().Instantiate(name, input));
            Value = parameter.Value;
            DbType = parameter.GetType().GetCustomAttributes(typeof(TypeMappingAttribute), true)
                .Cast<TypeMappingAttribute>().Single().DbType;
            Size = (parameter as IParameterSizable)?.Size ?? 0;
            Precision = (parameter as IParameterPrecisionable)?.Precision ?? 0;
            Scale = (parameter as IParameterPrecisionable)?.Scale ?? 0;
        }
    }
}
