using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class LoggingModuleTests
{
    [Fact]
    public void Constants_HaveCorrectValues()
    {
        Sharpy.Logging.DEBUG.Should().Be(10);
        Sharpy.Logging.INFO.Should().Be(20);
        Sharpy.Logging.WARNING.Should().Be(30);
        Sharpy.Logging.ERROR.Should().Be(40);
        Sharpy.Logging.CRITICAL.Should().Be(50);
    }

    [Fact]
    public void GetLogger_ReturnsSameInstance()
    {
        var logger1 = Sharpy.Logging.GetLogger("test_same");
        var logger2 = Sharpy.Logging.GetLogger("test_same");
        logger1.Should().BeSameAs(logger2);
    }

    [Fact]
    public void GetLogger_DifferentNames_ReturnsDifferentInstances()
    {
        var logger1 = Sharpy.Logging.GetLogger("test_diff_a");
        var logger2 = Sharpy.Logging.GetLogger("test_diff_b");
        logger1.Should().NotBeSameAs(logger2);
    }

    [Fact]
    public void Logger_Warning_OutputsToStderr()
    {
        var logger = new Sharpy.Logger("test_output");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Warning("test message");
            sw.ToString().TrimEnd().Should().Be("WARNING:test_output:test message");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_LevelFiltering_BelowLevel()
    {
        var logger = new Sharpy.Logger("test_filter");
        logger.SetLevel(Sharpy.Logging.ERROR);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Warning("should not appear");
            sw.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_LevelFiltering_AtLevel()
    {
        var logger = new Sharpy.Logger("test_at");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Warning("at level");
            sw.ToString().TrimEnd().Should().Be("WARNING:test_at:at level");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_OutputFormat()
    {
        var logger = new Sharpy.Logger("myapp");
        logger.SetLevel(Sharpy.Logging.DEBUG);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Debug("debug msg");
            logger.Info("info msg");
            logger.Error("error msg");
            logger.Critical("critical msg");

            var output = sw.ToString();
            output.Should().Contain("DEBUG:myapp:debug msg");
            output.Should().Contain("INFO:myapp:info msg");
            output.Should().Contain("ERROR:myapp:error msg");
            output.Should().Contain("CRITICAL:myapp:critical msg");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_DefaultLevel_IsWarning()
    {
        var logger = new Sharpy.Logger("test_default");

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Info("should not appear");
            logger.Warning("should appear");

            var output = sw.ToString();
            output.Should().NotContain("INFO");
            output.Should().Contain("WARNING");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }
}
