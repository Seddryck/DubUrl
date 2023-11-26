using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.MicroOrm;
using NUnit.Framework;

namespace DubUrl.Testing.MicroOrm;

public class NoneReflectionCacheTest
{
    private record class Customer(string Name, DateOnly BirthDate) { }

    [Test]
    public void Add_Any_DoesNotThrow()
        => Assert.That(() => new NoneReflectionCache().Add<Customer>(Array.Empty<PropertyInfo>(), Array.Empty<FieldInfo>())
                        , Throws.Nothing);

    [Test]
    public void Clear_Any_DoesNotThrow()
        => Assert.That(() => new NoneReflectionCache().Clear(), Throws.Nothing);

    [Test]
    public void Exists_Any_False()
    {
        var cache = new NoneReflectionCache();
        Assert.That(cache.Exists<Customer>(), Is.False);
        cache.Add<Customer>(Array.Empty<PropertyInfo>(), Array.Empty<FieldInfo>());
        Assert.That(cache.Exists<Customer>(), Is.False);
    }

    [Test]
    public void Get_Any_ThrowsNotImplemented()
        => Assert.That(() => new NoneReflectionCache().Get<Customer>(), Throws.TypeOf<NotImplementedException>());

    [Test]
    public void Remove_Any_DoesNotThrow()
        => Assert.That(() => new NoneReflectionCache().Remove<Customer>(), Throws.Nothing);
}
