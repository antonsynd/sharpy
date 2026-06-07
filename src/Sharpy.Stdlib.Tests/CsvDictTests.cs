using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class CsvDictTests
{
    // -------------------------------------------------------------------------
    // CsvDictReader
    // -------------------------------------------------------------------------

    [Fact]
    public void DictReader_AutoDetectsFieldnamesFromFirstRow()
    {
        var lines = new string[] { "name,age", "Alice,30" };
        var reader = Sharpy.CsvModule.DictReader(lines);

        // Fieldnames is null until iteration starts
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        reader.Fieldnames.IsSome.Should().BeTrue();
        reader.Fieldnames.Unwrap()[0].Should().Be("name");
        reader.Fieldnames.Unwrap()[1].Should().Be("age");
    }

    [Fact]
    public void DictReader_AutoDetect_RowHasCorrectValues()
    {
        var lines = new string[] { "name,age", "Alice,30", "Bob,25" };
        var reader = Sharpy.CsvModule.DictReader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(2);
        rows[0]["name"].Should().Be("Alice");
        rows[0]["age"].Should().Be("30");
        rows[1]["name"].Should().Be("Bob");
        rows[1]["age"].Should().Be("25");
    }

    [Fact]
    public void DictReader_ExplicitFieldnames_FirstRowIsData()
    {
        // When fieldnames are supplied, NO row is consumed as headers
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "age" });
        var lines = new string[] { "Alice,30", "Bob,25" };
        var reader = Sharpy.CsvModule.DictReader(lines, fieldnames);
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(2);
        rows[0]["name"].Should().Be("Alice");
        rows[0]["age"].Should().Be("30");
    }

    [Fact]
    public void DictReader_ExplicitFieldnames_Accessible_BeforeIteration()
    {
        var fieldnames = new Sharpy.List<string>(new string[] { "x", "y" });
        var lines = new string[] { "1,2" };
        var reader = Sharpy.CsvModule.DictReader(lines, fieldnames);

        // Fieldnames should be set even before iteration
        reader.Fieldnames.IsSome.Should().BeTrue();
        reader.Fieldnames.Unwrap()[0].Should().Be("x");
        reader.Fieldnames.Unwrap()[1].Should().Be("y");
    }

    [Fact]
    public void DictReader_MissingField_ProducesEmptyString()
    {
        // Row has fewer fields than fieldnames; missing field should be ""
        var lines = new string[] { "name,age,city", "Alice,30" };
        var reader = Sharpy.CsvModule.DictReader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(1);
        rows[0]["name"].Should().Be("Alice");
        rows[0]["age"].Should().Be("30");
        rows[0]["city"].Should().Be("");
    }

    [Fact]
    public void DictReader_ExtraFields_AreDropped()
    {
        // Row has more fields than fieldnames; extra fields are silently dropped
        var lines = new string[] { "name,age", "Alice,30,extra_value" };
        var reader = Sharpy.CsvModule.DictReader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(1);
        rows[0].Should().HaveCount(2);
        rows[0].ContainsKey("name").Should().BeTrue();
        rows[0].ContainsKey("age").Should().BeTrue();
    }

    [Fact]
    public void DictReader_QuotedFields_ParsedCorrectly()
    {
        var lines = new string[] { "name,desc", "Alice,\"hello, world\"" };
        var reader = Sharpy.CsvModule.DictReader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0]["desc"].Should().Be("hello, world");
    }

    [Fact]
    public void DictReader_NullLines_ThrowsTypeError()
    {
        Action act = () => Sharpy.CsvModule.DictReader(null!);
        act.Should().Throw<Sharpy.TypeError>();
    }

    [Fact]
    public void DictReader_SingleColumn_Works()
    {
        var lines = new string[] { "value", "42", "99" };
        var reader = Sharpy.CsvModule.DictReader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(2);
        rows[0]["value"].Should().Be("42");
        rows[1]["value"].Should().Be("99");
    }

    // -------------------------------------------------------------------------
    // CsvDictWriter
    // -------------------------------------------------------------------------

    [Fact]
    public void DictWriter_Fieldnames_Property_ReturnsFieldnames()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "age" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);

        writer.Fieldnames[0].Should().Be("name");
        writer.Fieldnames[1].Should().Be("age");
    }

    [Fact]
    public void DictWriter_Writeheader_WritesFieldnames()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "age", "city" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);
        writer.Writeheader();

        sw.ToString().TrimEnd().Should().Be("name,age,city");
    }

    [Fact]
    public void DictWriter_Writerow_WritesValuesInFieldOrder()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "age" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);

        var row = new Sharpy.Dict<string, string>();
        row["name"] = "Alice";
        row["age"] = "30";
        writer.Writerow(row);

        sw.ToString().TrimEnd().Should().Be("Alice,30");
    }

    [Fact]
    public void DictWriter_Writerow_MissingKey_WritesEmptyString()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "age", "city" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);

        var row = new Sharpy.Dict<string, string>();
        row["name"] = "Alice";
        row["age"] = "30";
        // "city" key is absent
        writer.Writerow(row);

        sw.ToString().TrimEnd().Should().Be("Alice,30,");
    }

    [Fact]
    public void DictWriter_Writerow_FieldWithComma_IsQuoted()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "address" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);

        var row = new Sharpy.Dict<string, string>();
        row["name"] = "Alice";
        row["address"] = "123 Main St, Springfield";
        writer.Writerow(row);

        var output = sw.ToString().TrimEnd();
        output.Should().Contain("\"123 Main St, Springfield\"");
    }

    [Fact]
    public void DictWriter_Writerows_WritesMultipleRows()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "age" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);

        var row1 = new Sharpy.Dict<string, string>();
        row1["name"] = "Alice";
        row1["age"] = "30";
        var row2 = new Sharpy.Dict<string, string>();
        row2["name"] = "Bob";
        row2["age"] = "25";

        writer.Writerows(new Sharpy.Dict<string, string>[] { row1, row2 });

        var lines = sw.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("Alice,30");
        lines[1].Should().Be("Bob,25");
    }

    [Fact]
    public void DictWriter_WriteheaderThenRows_ProducesFullCsv()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "score" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);

        writer.Writeheader();

        var row = new Sharpy.Dict<string, string>();
        row["name"] = "Alice";
        row["score"] = "100";
        writer.Writerow(row);

        var lines = sw.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("name,score");
        lines[1].Should().Be("Alice,100");
    }

    [Fact]
    public void DictWriter_RoundTrip_DictWriterThenDictReader()
    {
        var sw = new StringWriter();
        var fieldnames = new Sharpy.List<string>(new string[] { "name", "age" });
        var writer = Sharpy.CsvModule.DictWriter(sw, fieldnames);

        writer.Writeheader();

        var row1 = new Sharpy.Dict<string, string>();
        row1["name"] = "Alice";
        row1["age"] = "30";
        writer.Writerow(row1);

        var csv = sw.ToString();
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var reader = Sharpy.CsvModule.DictReader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.Dict<string, string>>();
        foreach (var r in reader)
        {
            rows.Add(r);
        }

        rows.Should().HaveCount(1);
        rows[0]["name"].Should().Be("Alice");
        rows[0]["age"].Should().Be("30");
    }

    [Fact]
    public void DictWriter_NullOutput_ThrowsTypeError()
    {
        var fieldnames = new Sharpy.List<string>(new string[] { "name" });
        Action act = () => Sharpy.CsvModule.DictWriter(null!, fieldnames);
        act.Should().Throw<Sharpy.TypeError>();
    }

    [Fact]
    public void DictWriter_NullFieldnames_ThrowsTypeError()
    {
        var sw = new StringWriter();
        Action act = () => Sharpy.CsvModule.DictWriter(sw, null!);
        act.Should().Throw<Sharpy.TypeError>();
    }
}
