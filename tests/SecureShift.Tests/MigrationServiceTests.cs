using SecureShift.ConsoleApp.Services;

namespace SecureShift.Tests;

/// <summary>Unit tests (no database): migration file names, checksums, BEGIN/COMMIT detection, settings.</summary>
public class MigrationServiceTests
{
    [Theory]
    [InlineData("V002__add_user_nickname.sql", 2, "add_user_nickname")]
    [InlineData("V010__x.sql", 10, "x")]
    [InlineData("V1234__big_number.sql", 1234, "big_number")]
    public void Valid_file_names_are_parsed(string fileName, int version, string name)
    {
        Assert.True(MigrationService.TryParseFileName(fileName, out var v, out var n));
        Assert.Equal(version, v);
        Assert.Equal(name, n);
    }

    [Theory]
    [InlineData("V2__too_short.sql")]          // needs 3 digits
    [InlineData("V002_one_underscore.sql")]
    [InlineData("V002__has-dash.sql")]
    [InlineData("v002__lowercase_v.sql")]
    [InlineData("V002__name.txt")]
    [InlineData("002__no_v.sql")]
    public void Invalid_file_names_are_rejected(string fileName)
    {
        Assert.False(MigrationService.TryParseFileName(fileName, out _, out _));
    }

    [Fact]
    public void Checksum_ignores_line_endings_and_bom()
    {
        var lf = "ALTER TABLE dev.\"Users\" ADD COLUMN \"X\" INT;\nSELECT 1;\n";
        var crlf = "﻿ALTER TABLE dev.\"Users\" ADD COLUMN \"X\" INT;\r\nSELECT 1;\r\n";
        Assert.Equal(MigrationService.ComputeChecksum(lf), MigrationService.ComputeChecksum(crlf));
        Assert.Equal(64, MigrationService.ComputeChecksum(lf).Length);
        Assert.NotEqual(MigrationService.ComputeChecksum(lf), MigrationService.ComputeChecksum(lf + " "));
    }

    [Theory]
    [InlineData("BEGIN;\nALTER TABLE dev.\"Users\" ADD COLUMN \"X\" INT;\nCOMMIT;", true)]
    [InlineData("begin transaction;\nSELECT 1;", true)]
    [InlineData("START TRANSACTION;", true)]
    [InlineData("SELECT 1;\nROLLBACK;", true)]
    [InlineData("ALTER TABLE dev.\"Users\" ADD COLUMN \"X\" INT;", false)]
    [InlineData("DO $$\nBEGIN\n  RAISE NOTICE 'hi';\nEND;\n$$;", false)]   // a PL/pgSQL block is fine
    public void Transaction_statements_are_detected(string sql, bool expected)
    {
        Assert.Equal(expected, MigrationService.ContainsTransactionStatements(sql));
    }

    [Fact]
    public void Migration_files_are_loaded_in_version_order_and_problems_reported()
    {
        var root = Directory.CreateTempSubdirectory("secureshift-mig-");
        try
        {
            var migrations = Directory.CreateDirectory(Path.Combine(root.FullName, "migrations"));
            File.WriteAllText(Path.Combine(migrations.FullName, "V010__ten.sql"), "SELECT 10;");
            File.WriteAllText(Path.Combine(migrations.FullName, "V002__two.sql"), "SELECT 2;");
            File.WriteAllText(Path.Combine(migrations.FullName, "V003__has_tx.sql"), "BEGIN;\nSELECT 3;\nCOMMIT;");
            File.WriteAllText(Path.Combine(migrations.FullName, "V001__reserved.sql"), "SELECT 1;");
            File.WriteAllText(Path.Combine(migrations.FullName, "bad name.sql"), "SELECT 0;");

            var service = new MigrationService(new DbSettings(null), new SqlScriptFiles(root.FullName));
            var (files, problems) = service.LoadFiles();

            Assert.Equal([2, 10], files.Select(f => f.Version));
            Assert.Equal(3, problems.Count);
            Assert.Contains(problems, p => p.Contains("V003__has_tx.sql"));
            Assert.Contains(problems, p => p.Contains("V001__reserved.sql"));
            Assert.Contains(problems, p => p.Contains("bad name.sql"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Host=YOUR_HOST;Port=5432;Database=YOUR_DATABASE;Username=YOUR_USERNAME;Password=YOUR_PASSWORD", false)]
    [InlineData("Host=db.example;Port=5432;Database=secureshift;Username=app;Password=x", true)]
    public void Placeholder_connection_strings_are_not_accepted(string? value, bool configured)
    {
        var settings = new DbSettings(value);
        Assert.Equal(configured, settings.IsConfigured);
        if (!configured) Assert.Throws<DbConfigurationException>(() => settings.GetConnectionString());
    }

    [Fact]
    public void The_sql_scripts_are_copied_next_to_the_app()
    {
        var files = SqlScriptFiles.Default();
        foreach (var name in new[] { SqlScriptFiles.CreateScript, SqlScriptFiles.SeedScript, SqlScriptFiles.VerifyScript, SqlScriptFiles.HistoryScript })
            Assert.True(File.Exists(files.PathOf(name)), name);
    }
}
