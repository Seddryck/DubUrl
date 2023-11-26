using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace DubUrl.MicroOrm;

internal static partial class IDataReaderExtensions
{
    /// <summary>
    ///     An IDataReader extension method that converts the @this to an entity.
    /// </summary>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// <param name="this">The @this to act on.</param>
    /// <returns>@this as a T.</returns>
    public static T ToEntity<T>(this IDataReader @this, IReflectionCache reflectionCache) where T : new()
    {
        PropertyInfo[] properties;
        FieldInfo[] fields;
        if (!reflectionCache.Exists<T>())
        {
            Type type = typeof(T);
            properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            reflectionCache.Add<T>(properties, fields);
        }
        else
            (properties, fields) = reflectionCache.Get<T>();

        var entity = new T();

        var hash = new HashSet<string>(Enumerable.Range(0, @this.FieldCount)
            .Select(@this.GetName));

        foreach (PropertyInfo property in properties)
        {
            if (hash.Contains(property.Name))
            {
                Type valueType = property.PropertyType;
                property.SetValue(entity, @this[property.Name].To(valueType), null);
            }
        }

        foreach (FieldInfo field in fields)
        {
            if (hash.Contains(field.Name))
            {
                Type valueType = field.FieldType;
                field.SetValue(entity, @this[field.Name].To(valueType));
            }
        }

        return entity;
    }

    /// <summary>
    ///     An IDataReader extension method that converts the @this to an expando object.
    /// </summary>
    /// <param name="this">The @this to act on.</param>
    /// <returns>@this as a dynamic.</returns>
    public static dynamic ToExpandoObject(this IDataReader @this)
    {
        var entity = new ExpandoObject();
        for (int i = 0; i < @this.FieldCount; i++)
            entity.TryAdd(@this.GetName(i), @this.GetValue(i));

        return entity;
    }
}
