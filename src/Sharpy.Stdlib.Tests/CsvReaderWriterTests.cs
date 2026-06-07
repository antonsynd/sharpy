using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class CsvReaderWriterTests
{
    // -------------------------------------------------------------------------
    // CsvReader — basic iteration
    // -------------------------------------------------------------------------

    [Fact]
    public void Reader_EmptyLines_ReturnsNoRows()
    {
        var reader = Sharpy.CsvModule.Reader(Array.Empty<string>());
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().BeEmpty();
    }

    [Fact]
    public void Reader_SingleField_ReturnsSingleElementRow()
    {
        var lines = new string[] { "hello" };
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(1);
        rows[0].Should().HaveCount(1);
        rows[0][0].Should().Be("hello");
    }

    [Fact]
    public void Reader_EmptyFields_ParsesMiddleEmptyField()
    {
        // a,,b => ["a", "", "b"]
        var lines = new string[] { "a,,b" };
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0].Should().HaveCount(3);
        rows[0][0].Should().Be("a");
        rows[0][1].Should().Be("");
        rows[0][2].Should().Be("b");
    }

    [Fact]
    public void Reader_TrailingComma_ProducesEmptyLastField()
    {
        // "a,b," => ["a", "b", ""]
        var lines = new string[] { "a,b," };
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0].Should().HaveCount(3);
        rows[0][2].Should().Be("");
    }

    [Fact]
    public void Reader_LeadingComma_ProducesEmptyFirstField()
    {
        // ",a,b" => ["", "a", "b"]
        var lines = new string[] { ",a,b" };
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0].Should().HaveCount(3);
        rows[0][0].Should().Be("");
        rows[0][1].Should().Be("a");
    }

    [Fact]
    public void Reader_AllEmpty_SingleRowWithOneEmptyField()
    {
        // Empty line => [""]
        var lines = new string[] { "" };
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(1);
        rows[0].Should().HaveCount(1);
        rows[0][0].Should().Be("");
    }

    [Fact]
    public void Reader_QuotedFieldContainingComma_IsOneField()
    {
        // Already in CsvModuleTests but as coverage of ParseLine internal path
        var lines = new string[] { "\"a,b\",c" };
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0].Should().HaveCount(2);
        rows[0][0].Should().Be("a,b");
        rows[0][1].Should().Be("c");
    }

    [Fact]
    public void Reader_QuotedFieldWithDoubleQuote_UnescapesQuote()
    {
        // "he said ""hi""" => he said "hi"
        var lines = new string[] { "\"he said \"\"hi\"\"\"" };
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0][0].Should().Be("he said \"hi\"");
    }

    [Fact]
    public void Reader_LineNum_StartsAtZero()
    {
        var lines = new string[] { "a,b", "c,d" };
        var reader = Sharpy.CsvModule.Reader(lines);
        reader.LineNum.Should().Be(0);
    }

    [Fact]
    public void Reader_LineNum_IncrementsDuringIteration()
    {
        var lines = new string[] { "a,b", "c,d", "e,f" };
        var reader = Sharpy.CsvModule.Reader(lines);
        var lineNums = new System.Collections.Generic.List<int>();
        foreach (var row in reader)
        {
            lineNums.Add(reader.LineNum);
        }

        lineNums.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Reader_NullLines_ThrowsTypeError()
    {
        Action act = () => Sharpy.CsvModule.Reader(null!);
        act.Should().Throw<Sharpy.TypeError>();
    }

    // -------------------------------------------------------------------------
    // CsvWriter — additional coverage
    // -------------------------------------------------------------------------

    [Fact]
    public void Writer_EmptyRow_WritesNewlineOnly()
    {
        var sw = new StringWriter();
        var writer = Sharpy.CsvModule.Writer(sw);
        writer.Writerow(Array.Empty<string>());
        sw.ToString().Should().Be(Environment.NewLine);
    }

    [Fact]
    public void Writer_FieldWithQuote_EscapesQuote()
    {
        var sw = new StringWriter();
        var writer = Sharpy.CsvModule.Writer(sw);
        writer.Writerow(new string[] { "say \"hello\"" });
        // Field containing a quote should be quoted with internal quotes doubled
        sw.ToString().TrimEnd().Should().Be("\"say \"\"hello\"\"\"");
    }

    [Fact]
    public void Writer_FieldWithNewline_QuotesField()
    {
        var sw = new StringWriter();
        var writer = Sharpy.CsvModule.Writer(sw);
        writer.Writerow(new string[] { "line1\nline2" });
        var output = sw.ToString().TrimEnd();
        output.Should().StartWith("\"");
        output.Should().Contain("line1\nline2");
    }

    [Fact]
    public void Writer_PlainField_NotQuoted()
    {
        var sw = new StringWriter();
        var writer = Sharpy.CsvModule.Writer(sw);
        writer.Writerow(new string[] { "simple" });
        sw.ToString().TrimEnd().Should().Be("simple");
    }

    [Fact]
    public void Writer_Writerows_EmptyList_WritesNothing()
    {
        var sw = new StringWriter();
        var writer = Sharpy.CsvModule.Writer(sw);
        writer.Writerows(new Sharpy.List<Sharpy.List<string>>());
        sw.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Writer_NullOutput_ThrowsTypeError()
    {
        Action act = () => Sharpy.CsvModule.Writer(null!);
        act.Should().Throw<Sharpy.TypeError>();
    }

    [Fact]
    public void Writer_RoundTrip_WriteAndReadBack()
    {
        // Write rows then re-read them with Reader to verify round-trip
        var sw = new StringWriter();
        var writer = Sharpy.CsvModule.Writer(sw);
        writer.Writerow(new string[] { "name", "city" });
        writer.Writerow(new string[] { "Alice", "New York" });
        writer.Writerow(new string[] { "Bob", "San Francisco, CA" });

        var csv = sw.ToString();
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var reader = Sharpy.CsvModule.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(3);
        rows[0][0].Should().Be("name");
        rows[0][1].Should().Be("city");
        rows[1][0].Should().Be("Alice");
        rows[1][1].Should().Be("New York");
        rows[2][0].Should().Be("Bob");
        rows[2][1].Should().Be("San Francisco, CA");
    }
}
