using NUnit.Framework;
using Dapper;
using DubUrl.QA.Dapper;
using DubUrl.Registering;
using DubUrl.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace DubUrl.QA.Timescale
{
    [Category("Timescale")]
    [Category("AdoProvider")]
    public class AdoProvider : Postgresql.AdoProvider
    {
        public override string ConnectionString
        {
            get => $"ts://postgres:Password12!@localhost/DubUrl";
        }
    }
}