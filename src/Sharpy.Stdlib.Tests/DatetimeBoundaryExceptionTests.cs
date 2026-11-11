using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// The datetime boundary cells the exception-escape sweep found leaking CLR exceptions, each pinned to the type and
/// message python 3.15.2 raises (measured with <c>python3.15 -c</c>, the cell's python spelling in its row). The
/// sweep (<c>Conformance/ExceptionEscape</c>) guards the type over every member; these pin the message too.
/// </summary>
public class DatetimeBoundaryExceptionTests
{
    public static IEnumerable<object[]> Cells() => new[]
    {
        // date.max + timedelta(days=1)
        Cell(() => new Date(9999, 12, 31) + new Timedelta(days: 1), typeof(OverflowError), "date value out of range"),
        // date.min - timedelta(days=1)
        Cell(() => new Date(1, 1, 1) - new Timedelta(days: 1), typeof(OverflowError), "date value out of range"),
        // date(1,1,1) + timedelta(days=-1)
        Cell(() => new Date(1, 1, 1) + new Timedelta(days: -1), typeof(OverflowError), "date value out of range"),
        // datetime.max + timedelta(days=1)
        Cell(() => new DateTime(9999, 12, 31, 23, 59, 59, 999999) + new Timedelta(days: 1), typeof(OverflowError), "date value out of range"),
        // datetime.min - timedelta(microseconds=1)
        Cell(() => new DateTime(1, 1, 1) - new Timedelta(microseconds: 1), typeof(OverflowError), "date value out of range"),
        // datetime.max.replace(tzinfo=timezone.utc).astimezone(timezone(timedelta(hours=1)))
        Cell(() => new DateTime(9999, 12, 31, 23, 59, 59, 999999, Timezone.Utc).Astimezone(new Timezone(new Timedelta(hours: 1))),
            typeof(OverflowError), "date value out of range"),
        // datetime.min.replace(tzinfo=timezone.utc).astimezone(timezone(timedelta(hours=-1)))
        Cell(() => new DateTime(1, 1, 1, tzinfo: Timezone.Utc).Astimezone(new Timezone(new Timedelta(hours: -1))),
            typeof(OverflowError), "date value out of range"),
        // timedelta(days=2147483647)
        Cell(() => new Timedelta(days: int.MaxValue), typeof(OverflowError), "days=2147483647; must have magnitude <= 999999999"),
        // timedelta(days=-2147483648)
        Cell(() => new Timedelta(days: int.MinValue), typeof(OverflowError), "days=-2147483648; must have magnitude <= 999999999"),
        // timedelta(days=1) * (2**31 - 1)
        Cell(() => new Timedelta(days: 1) * int.MaxValue, typeof(OverflowError), "days=2147483647; must have magnitude <= 999999999"),
        // timedelta(days=1) / 0
        Cell(() => new Timedelta(days: 1) / 0, typeof(ZeroDivisionError), "division by zero"),
        // date.fromisoformat('not-a-date')
        Cell(() => Date.Fromisoformat("not-a-date"), typeof(ValueError), "Invalid isoformat string: 'not-a-date'"),
        // date.fromisoformat('')
        Cell(() => Date.Fromisoformat(""), typeof(ValueError), "Invalid isoformat string: ''"),
        // date.fromisoformat('2020-1-1')
        Cell(() => Date.Fromisoformat("2020-1-1"), typeof(ValueError), "Invalid isoformat string: '2020-1-1'"),
        // datetime.fromisoformat('not-a-date')
        Cell(() => DateTime.Fromisoformat("not-a-date"), typeof(ValueError), "Invalid isoformat string: 'not-a-date'"),
        // timezone(timedelta(hours=24))
        Cell(() => new Timezone(new Timedelta(hours: 24)), typeof(ValueError),
            "offset must be a timedelta strictly between -timedelta(hours=24) and timedelta(hours=24), not datetime.timedelta(days=1)"),
        // timezone(timedelta(hours=-24))
        Cell(() => new Timezone(new Timedelta(hours: -24)), typeof(ValueError),
            "offset must be a timedelta strictly between -timedelta(hours=24) and timedelta(hours=24), not datetime.timedelta(days=-1)"),
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void BoundaryCell_RaisesPythonsException(Func<object> cell, Type expected, string message)
    {
        var ex = Record.Exception(cell);
        Assert.NotNull(ex);
        Assert.Equal(expected, ex!.GetType());
        Assert.Equal(message, ex.Message);
    }

    /// <summary>The month-range cell: python 3.15's type; its text is #2461's pre-3.15 spelling (see the report).</summary>
    [Fact]
    public void Fromisoformat_OutOfRangeMonth_IsValueError()
    {
        Assert.Throws<ValueError>(() => Date.Fromisoformat("2020-13-01"));
        Assert.Throws<ValueError>(() => DateTime.Fromisoformat("2020-13-01"));
    }

    /// <summary>The neighbours that python accepts stay accepted: the basic form, the extremes, and an aware min timestamp.</summary>
    [Fact]
    public void Accepted_Neighbours()
    {
        Assert.Equal(new Date(2020, 1, 1), Date.Fromisoformat("20200101"));
        Assert.Equal(new Date(2020, 1, 1), Date.Fromisoformat("2020-01-01"));
        Assert.Equal(new Date(9999, 12, 31), new Date(9999, 12, 30) + new Timedelta(days: 1));
        Assert.Equal(new Date(9999, 12, 31), new Date(9999, 12, 31) + new Timedelta(hours: 23));
        Assert.Equal("23:59:00", new Timezone(new Timedelta(hours: 23, minutes: 59)).Utcoffset().ToString());
        // datetime.min.replace(tzinfo=timezone(timedelta(hours=1))).timestamp() == -62135600400.0
        Assert.Equal(-62135600400.0, new DateTime(1, 1, 1, tzinfo: new Timezone(new Timedelta(hours: 1))).Timestamp());
    }

    private static object[] Cell(Func<object> cell, Type type, string message) => new object[] { cell, type, message };
}
