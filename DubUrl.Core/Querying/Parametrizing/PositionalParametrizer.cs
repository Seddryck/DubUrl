using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

public class PositionalParametrizer : IParametrizer
{
    public PositionalParametrizer()
    { }

    public virtual IDbDataParameter CreateParameter(IDbCommand command, DubUrlParameter parameter)
    {
        var param = command.CreateParameter();
        
        param.Direction = ParameterDirection.Input;
        (param.Precision, param.Scale) = GetPrecision(parameter as IParameterPrecisionable);
        param.Size = GetSize(parameter as IParameterSizable) ?? default;
        param.DbType = GetDbType(parameter.GetType())
            ?? throw new ArgumentException($"Parameter '{parameter.Name}' is of type '{parameter.GetType().Name}' but this type is not associated with an attribute of type '{nameof(TypeMappingAttribute)}'"); ;

        param.Value = GetValue(parameter.Value);
        return param;
    }

    protected virtual (byte precision, byte scale) GetPrecision(IParameterPrecisionable? precisionable)
        => (precisionable?.Precision ?? default, precisionable?.Scale ?? default);

    protected virtual int? GetSize(IParameterSizable? sizable)
        => sizable?.Size;

    protected virtual DbType? GetDbType(Type paramType)
        => paramType.GetCustomAttribute<TypeMappingAttribute>()?.DbType;

    protected virtual object GetValue(object? value)
        => value ?? DBNull.Value;
}
