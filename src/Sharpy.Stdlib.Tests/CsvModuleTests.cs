using System.Collections.Generic;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class CsvModuleTests
{
    [Fact]
    public void Reader_SimpleLine()
    {
        var lines = new string[] { "a,b,c" };
        var reader = Sharpy.Csv.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(1);
        rows[0][0].Should().Be("a");
        rows[0][1].Should().Be("b");
        rows[0][2].Should().Be("c");
    }

    [Fact]
    public void Reader_QuotedFieldWithComma()
    {
        var lines = new string[] { "a,\"hello, world\",c" };
        var reader = Sharpy.Csv.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0][0].Should().Be("a");
        rows[0][1].Should().Be("hello, world");
        rows[0][2].Should().Be("c");
    }

    [Fact]
    public void Reader_EscapedQuote()
    {
        // CSV: a,"say ""hello""",c => fields: a | say "hello" | c
        var lines = new string[] { "a,\"say \"\"hello\"\"\",c" };
        var reader = Sharpy.Csv.Reader(lines);
        var rows = new System.Collections.Generic.List<Sharpy.List<string>>();
        foreach (var row in reader)
        {
            rows.Add(row);
        }

        rows[0][1].Should().Be("say \"hello\"");
    }

    [Fact]
    public void Reader_MultipleRows()
    {
        var lines = new string[] { "a,b", "c,d", "e,f" };
        var reader = Sharpy.Csv.Reader(lines);
        int count = 0;
        foreach (var row in reader)
        {
            count++;
        }

        count.Should().Be(3);
    }

    [Fact]
    public void Writer_SimpleRow()
    {
        var sw = new StringWriter();
        var writer = Sharpy.Csv.Writer(sw);
        writer.Writerow(new string[] { "a", "b", "c" });
        sw.ToString().Should().Be("a,b,c" + System.Environment.NewLine);
    }

    [Fact]
    public void Writer_QuotesFieldWithComma()
    {
        var sw = new StringWriter();
        var writer = Sharpy.Csv.Writer(sw);
        writer.Writerow(new string[] { "hello, world", "test" });
        // Should contain quoted field
        sw.ToString().Should().Contain("\"hello, world\"");
    }

    [Fact]
    public void Writer_Writerows_MultipleRows()
    {
        var sw = new StringWriter();
        var writer = Sharpy.Csv.Writer(sw);
        var rows = new System.Collections.Generic.List<string[]>
        {
            new string[] { "a", "b" },
            new string[] { "c", "d" }
        };
        writer.Writerows(rows);

        var lines = sw.ToString().Split(System.Environment.NewLine, System.StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
    }

    [Fact]
    public void Constants_HaveCorrectValues()
    {
        Sharpy.Csv.QUOTE_ALL.Should().Be(1);
        Sharpy.Csv.QUOTE_MINIMAL.Should().Be(0);
        Sharpy.Csv.QUOTE_NONE.Should().Be(3);
        Sharpy.Csv.QUOTE_NONNUMERIC.Should().Be(2);
    }

    [Fact]
    public void Reader_LineNum_TracksLinesRead()
    {
        var lines = new string[] { "a,b", "c,d", "e,f" };
        var reader = Sharpy.Csv.Reader(lines);
        reader.LineNum.Should().Be(0);

        int count = 0;
        foreach (var row in reader)
        {
            count++;
            reader.LineNum.Should().Be(count);
        }

        reader.LineNum.Should().Be(3);
    }
}
