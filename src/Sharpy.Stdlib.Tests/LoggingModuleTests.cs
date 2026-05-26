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

    [Fact]
    public void Logger_HigherSeverity_PassesLowerThreshold()
    {
        // At threshold WARNING, the more severe ERROR and CRITICAL messages emit.
        var logger = new Sharpy.Logger("test_higher_sev");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Error("err msg");
            logger.Critical("crit msg");

            var output = sw.ToString();
            output.Should().Contain("ERROR:test_higher_sev:err msg");
            output.Should().Contain("CRITICAL:test_higher_sev:crit msg");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_AllLevelsAtOrAboveInfo_Emit()
    {
        var logger = new Sharpy.Logger("test_all_above_info");
        logger.SetLevel(Sharpy.Logging.INFO);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Debug("dbg suppressed");
            logger.Info("info shown");
            logger.Warning("warn shown");
            logger.Error("error shown");
            logger.Critical("critical shown");

            var output = sw.ToString();
            output.Should().NotContain("DEBUG");
            output.Should().Contain("INFO:test_all_above_info:info shown");
            output.Should().Contain("WARNING:test_all_above_info:warn shown");
            output.Should().Contain("ERROR:test_all_above_info:error shown");
            output.Should().Contain("CRITICAL:test_all_above_info:critical shown");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_EmptyMessage_EmitsPrefixOnly()
    {
        var logger = new Sharpy.Logger("test_empty_msg");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Warning("");
            sw.ToString().TrimEnd().Should().Be("WARNING:test_empty_msg:");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_MessageWithColons_PreservedVerbatim()
    {
        // Colons in the message must not be misinterpreted as field separators.
        var logger = new Sharpy.Logger("test_colons");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Warning("key:value:pair");
            sw.ToString().TrimEnd().Should().Be("WARNING:test_colons:key:value:pair");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_MultipleMessages_AppearInOrder()
    {
        var logger = new Sharpy.Logger("test_order");
        logger.SetLevel(Sharpy.Logging.DEBUG);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Debug("first");
            logger.Info("second");
            logger.Warning("third");

            var output = sw.ToString();
            var firstIdx = output.IndexOf("first", StringComparison.Ordinal);
            var secondIdx = output.IndexOf("second", StringComparison.Ordinal);
            var thirdIdx = output.IndexOf("third", StringComparison.Ordinal);

            firstIdx.Should().BeGreaterThanOrEqualTo(0);
            secondIdx.Should().BeGreaterThan(firstIdx);
            thirdIdx.Should().BeGreaterThan(secondIdx);
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_SetLevel_RaiseLevel_SuppressesLowerSeverity()
    {
        // Start permissive (DEBUG), then raise to ERROR; WARNING must be suppressed.
        var logger = new Sharpy.Logger("test_raise_level");
        logger.SetLevel(Sharpy.Logging.DEBUG);
        logger.SetLevel(Sharpy.Logging.ERROR);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Warning("should be suppressed after raise");
            logger.Error("should still appear");

            var output = sw.ToString();
            output.Should().NotContain("should be suppressed after raise");
            output.Should().Contain("should still appear");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }
}
