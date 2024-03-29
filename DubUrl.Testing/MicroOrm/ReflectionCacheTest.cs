﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.MicroOrm;
using NUnit.Framework;

namespace DubUrl.Testing.MicroOrm;

public class ReflectionCacheTest
{
    private record class Customer(string Name, DateOnly BirthDate) { }
    private record class Product(string Name, decimal Price) { }

    [Test]
    public void Add_Any_Available()
    {
        var cache = new ReflectionCache();
        Assert.That(cache.Exists<Customer>(), Is.False);
        cache.Add<Customer>([], []);
        Assert.That(cache.Exists<Customer>(), Is.True);
    }

    [Test]
    public void Clear_Any_NotAvailable()
    {
        var cache = new ReflectionCache();
        Assert.That(cache.Exists<Customer>(), Is.False);
        cache.Add<Customer>([], []);
        cache.Clear();
        Assert.That(cache.Exists<Customer>(), Is.False);
    }

    [Test]
    public void Exists_Any_False()
    {
        var cache = new ReflectionCache();
        Assert.Multiple(() =>
        {
            Assert.That(cache.Exists<Customer>(), Is.False);
            Assert.That(cache.Exists<Product>(), Is.False);
        });
        cache.Add<Customer>([], []);
        Assert.Multiple(() =>
        {
            Assert.That(cache.Exists<Customer>(), Is.True);
            Assert.That(cache.Exists<Product>(), Is.False);
        });
    }

    [Test]
    public void Get_Any_ThrowsNotImplemented()
    {
        var cache = new ReflectionCache();
        cache.Add<Customer>([], []);
        cache.Add<Product>([], []);
        var (x, y) = cache.Get<Customer>();
        Assert.Multiple(() =>
        {
            Assert.That(x, Is.Not.Null);
            Assert.That(y, Is.Not.Null);
        });
    }

    [Test]
    public void Remove_Any_DoesNotThrow()
    {
        var cache = new ReflectionCache();
        cache.Add<Customer>([], []);
        cache.Add<Product>([], []);
        cache.Remove<Customer>();
        Assert.Multiple(() =>
        {
            Assert.That(cache.Exists<Customer>(), Is.False);
            Assert.That(cache.Exists<Product>(), Is.True);
        });
    }
}
