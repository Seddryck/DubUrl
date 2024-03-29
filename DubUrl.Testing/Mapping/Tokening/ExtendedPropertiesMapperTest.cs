﻿using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;


namespace DubUrl.Testing.Mapping.Tokening;

internal class ExtendedPropertiesMapperTest
{
    private class FakeSpecificator : ISpecificator
    {
        public Dictionary<string, object> Properties { get; } = [];

        public string ConnectionString => throw new NotImplementedException();

        public bool AcceptKey(string keyword) => true;
        public void Execute(string keyword, object value)
            => Properties.Add(keyword, value);
        public IReadOnlyDictionary<string, object> ToReadOnlyDictionary() => throw new NotImplementedException();
    }


    [Test]
    public void Execute_ValueNoOption_Value()
    {
        var urlInfo = new UrlInfo() { Segments = string.Empty.Split('/') };
        var mapper = new ExtendedPropertiesMapper(["Excel 8.0"]);
        var specificator = new FakeSpecificator();
        mapper.Accept(specificator);
        mapper.Execute(urlInfo);

        Assert.That(specificator.Properties, Has.Count.EqualTo(1));
        Assert.That(specificator.Properties, Does.ContainKey(ExtendedPropertiesMapper.EXTENDED_PROPERTIES_KEYWORD));
        Assert.That(specificator.Properties[ExtendedPropertiesMapper.EXTENDED_PROPERTIES_KEYWORD], Is.EqualTo("Excel 8.0;"));
    }

    [Test]
    public void Execute_NoValueButOptions_Options()
    {
        var urlInfo = new UrlInfo() { Segments = string.Empty.Split('/')
            , Options = new Dictionary<string, string>() { { "HDR", "YES" }, { "IMEX", "1" } } };
        var mapper = new ExtendedPropertiesMapper([]);
        var specificator = new FakeSpecificator();
        mapper.Accept(specificator);
        mapper.Execute(urlInfo);

        Assert.That(specificator.Properties, Has.Count.EqualTo(1));
        Assert.That(specificator.Properties, Does.ContainKey(ExtendedPropertiesMapper.EXTENDED_PROPERTIES_KEYWORD));
        Assert.That(specificator.Properties[ExtendedPropertiesMapper.EXTENDED_PROPERTIES_KEYWORD], Is.EqualTo("HDR=YES;IMEX=1;"));
    }

    [Test]
    public void Execute_ValueAndOptions_ValueAndOptions()
    {
        var urlInfo = new UrlInfo()
        {
            Segments = string.Empty.Split('/')
            , Options = new Dictionary<string, string>() { { "HDR", "YES" }, { "IMEX", "1" } }
        };
        var mapper = new ExtendedPropertiesMapper(["Excel 8.0"]);
        var specificator = new FakeSpecificator();
        mapper.Accept(specificator);
        mapper.Execute(urlInfo);

        Assert.That(specificator.Properties, Has.Count.EqualTo(1));
        Assert.That(specificator.Properties, Does.ContainKey(ExtendedPropertiesMapper.EXTENDED_PROPERTIES_KEYWORD));
        Assert.That(specificator.Properties[ExtendedPropertiesMapper.EXTENDED_PROPERTIES_KEYWORD], Is.EqualTo("Excel 8.0;HDR=YES;IMEX=1;"));
    }
}
