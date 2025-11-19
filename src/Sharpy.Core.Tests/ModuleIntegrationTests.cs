using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Integration tests to verify that all the new modules and functions work together
/// </summary>
public class ModuleIntegrationTests
{
    [Fact]
    public void SysModule_HasExpectedMembers()
    {
        // Verify sys.argv exists
        Sharpy.Sys.Exports.Argv.Should().NotBeNull();

        // Verify sys.version exists
        Sharpy.Sys.Exports.Version.Should().NotBeNullOrEmpty();

        // Verify sys.platform exists
        Sharpy.Sys.Exports.Platform.Should().NotBeNullOrEmpty();

        // Verify sys.executable exists
        Sharpy.Sys.Exports.Executable.Should().NotBeNull();

        // Verify sys.path exists
        Sharpy.Sys.Exports.Path.Should().NotBeNull();

        // Verify sys.stdin exists
        Sharpy.Sys.Exports.Stdin.Should().NotBeNull();
    }

    [Fact]
    public void MathModule_BasicFunctions_WorkCorrectly()
    {
        // Test sqrt
        Sharpy.MathModule.Exports.Sqrt(16.0).Should().Be(4.0);

        // Test pow
        Sharpy.MathModule.Exports.Pow(2.0, 3.0).Should().Be(8.0);

        // Test floor and ceil
        Sharpy.MathModule.Exports.Floor(3.7).Should().Be(3.0);
        Sharpy.MathModule.Exports.Ceil(3.2).Should().Be(4.0);

        // Test trigonometric
        System.Math.Round(Sharpy.MathModule.Exports.Sin(Sharpy.MathModule.Exports.Pi / 2), 10).Should().Be(1.0);

        // Test constants
        Sharpy.MathModule.Exports.Pi.Should().BeApproximately(3.14159, 0.00001);
        Sharpy.MathModule.Exports.E.Should().BeApproximately(2.71828, 0.00001);

        // Test factorial
        Sharpy.MathModule.Exports.Factorial(5).Should().Be(120);

        // Test gcd
        Sharpy.MathModule.Exports.Gcd(48, 18).Should().Be(6);
    }

    [Fact]
    public void RandomModule_BasicFunctions_WorkCorrectly()
    {
        // Test random
        var rand1 = Sharpy.Random.Exports.Random();
        rand1.Should().BeInRange(0.0, 1.0);

        // Test randint
        var randInt = Sharpy.Random.Exports.Randint(1, 10);
        randInt.Should().BeInRange(1, 10);

        // Test choice
        var arr = new[] { 1, 2, 3, 4, 5 };
        var choice = Sharpy.Random.Exports.Choice(arr);
        arr.Should().Contain(choice);

        // Test uniform
        var uniform = Sharpy.Random.Exports.Uniform(1.0, 5.0);
        uniform.Should().BeInRange(1.0, 5.0);
    }

    [Fact]
    public void DatetimeModule_BasicFunctions_WorkCorrectly()
    {
        // Test DateObject
        var date = new Sharpy.DatetimeModule.DateObject(2024, 1, 15);
        date.Year.Should().Be(2024);
        date.Month.Should().Be(1);
        date.Day.Should().Be(15);

        // Test TimeObject
        var time = new Sharpy.DatetimeModule.TimeObject(14, 30, 0);
        time.Hour.Should().Be(14);
        time.Minute.Should().Be(30);

        // Test DateTimeObject
        var dt = new Sharpy.DatetimeModule.DateTimeObject(2024, 1, 15, 14, 30, 0);
        dt.Year.Should().Be(2024);
        dt.Month.Should().Be(1);
        dt.Day.Should().Be(15);
        dt.Hour.Should().Be(14);
        dt.Minute.Should().Be(30);

        // Test Now (should return a valid datetime)
        var now = Sharpy.DatetimeModule.DateTimeObject.Now();
        now.Should().NotBeNull();
        now.Year.Should().BeGreaterThan(2020);
    }

    [Fact]
    public void CollectionsModule_Deque_WorksCorrectly()
    {
        // Test Deque
        var deque = new Sharpy.Collections.Deque<int>();
        deque.Append(1);
        deque.Append(2);
        deque.Appendleft(0);

        deque.__Len__().Should().Be(3);

        var right = deque.Pop();
        right.Should().Be(2);

        var left = deque.Popleft();
        left.Should().Be(0);

        deque.__Len__().Should().Be(1);
    }

    [Fact]
    public void CollectionsModule_Counter_WorksCorrectly()
    {
        // Test Counter
        var items = new[] { "a", "b", "c", "a", "b", "a" };
        var counter = new Sharpy.Collections.Counter<string>(items);

        counter["a"].Should().Be(3);
        counter["b"].Should().Be(2);
        counter["c"].Should().Be(1);
        counter["d"].Should().Be(0); // Not present

        var mostCommon = counter.MostCommon(2);
        mostCommon.Should().HaveCount(2);
        mostCommon[0].Item1.Should().Be("a");
        mostCommon[0].Item2.Should().Be(3);
    }

    [Fact]
    public void CollectionsModule_DefaultDict_WorksCorrectly()
    {
        // Test DefaultDict
        var defaultDict = new Sharpy.Collections.DefaultDict<string, int>(() => 0);

        defaultDict["existing"] = 42;
        defaultDict["existing"].Should().Be(42);

        // Accessing non-existent key should return default value
        defaultDict["new"].Should().Be(0);
    }

}

