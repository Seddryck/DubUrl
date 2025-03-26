using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.BulkCopy.Engines;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using Moq;
using NUnit.Framework;

namespace DubUrl.BulkCopy.Testing.Engines;
internal class DuckDbAppenderFactoryTests
{
    [Test]
    public void CreateAppender_ConnectionOpenExistingTable_Success()
    {
        var factory = new DuckDbAppenderFactory();

        using var conn = new DuckDBConnection("DataSource = :memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Customer; CREATE TABLE Customer (id INTEGER PRIMARY KEY);";
        cmd.ExecuteNonQuery();

        var appender = factory.CreateAppender(conn, "Customer");
        Assert.That(appender, Is.Not.Null);
    }

    [Test]
    public void CreateAppender_ConnectionCloseExistingTable_Success()
    {
        var factory = new DuckDbAppenderFactory();

        using var conn = new DuckDBConnection($"DataSource = {Guid.NewGuid():N}.db");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Customer; CREATE TABLE Customer (id INTEGER PRIMARY KEY);";
        cmd.ExecuteNonQuery();
        conn.Close();

        var appender = factory.CreateAppender(conn, "Customer");
        Assert.That(appender, Is.Not.Null);
    }

    [Test]
    public void CreateAppender_DateOnly_Success()
    {
        var factory = new DuckDbAppenderFactory();
        using var conn = new DuckDBConnection($"DataSource = {Guid.NewGuid():N}.db");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Customer; CREATE TABLE Customer (birthDate DATE);";
        cmd.ExecuteNonQuery();
        conn.Close();

        var appender = factory.CreateAppender(conn, "Customer");
        Assert.That(appender, Is.Not.Null);
        var row = appender.CreateRow();
        Assert.DoesNotThrow(() => row.AppendValue(new DateOnly(2021, 1, 1)));
    }

    [Test]
    public void CreateAppender_TimeOnly_Success()
    {
        var factory = new DuckDbAppenderFactory();
        using var conn = new DuckDBConnection($"DataSource = {Guid.NewGuid():N}.db");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP TABLE IF EXISTS Customer; CREATE TABLE Customer (Wakeup TIME);";
        cmd.ExecuteNonQuery();
        conn.Close();

        var appender = factory.CreateAppender(conn, "Customer");
        Assert.That(appender, Is.Not.Null);
        var row = appender.CreateRow();
        Assert.DoesNotThrow(() => row.AppendValue(new TimeOnly(15, 18, 10)));
    }

    [Test]
    public void CreateAppender_NonExistingTable_Throws()
    {
        var factory = new DuckDbAppenderFactory();

        using var conn = new DuckDBConnection("DataSource = :memory:");
        conn.Open();

        var ex = Assert.Throws<DuckDBException>(() => factory.CreateAppender(conn, "Customer"));
        Assert.That(ex.Message, Does.Contain("Customer"));
    }
}
