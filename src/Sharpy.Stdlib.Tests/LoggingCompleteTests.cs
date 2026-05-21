using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

// Tests in this class construct Logger instances directly (not via GetLogger)
// to avoid sharing state with LoggingModuleTests or each other.
// Each test uses a unique logger name to prevent cross-test interference.
public class LoggingCompleteTests
{
    // -------------------------------------------------------------------------
    // Logger creation via GetLogger
    // -------------------------------------------------------------------------

    [Fact]
    public void GetLogger_NoArgument_ReturnsRootLogger()
    {
        // GetLogger() should return the same instance as GetLogger("root")
        var root1 = Sharpy.Logging.GetLogger();
        var root2 = Sharpy.Logging.GetLogger("root");
        root1.Should().BeSameAs(root2);
    }

    [Fact]
    public void GetLogger_UniqueNamesReturnDifferentInstances()
    {
        var a = Sharpy.Logging.GetLogger("lc_unique_a");
        var b = Sharpy.Logging.GetLogger("lc_unique_b");
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void GetLogger_SameNameReturnsCachedInstance()
    {
        var first = Sharpy.Logging.GetLogger("lc_cached_test");
        var second = Sharpy.Logging.GetLogger("lc_cached_test");
        first.Should().BeSameAs(second);
    }

    // -------------------------------------------------------------------------
    // Level constants
    // -------------------------------------------------------------------------

    [Fact]
    public void Constants_DEBUG_LessThan_INFO()
    {
        Sharpy.Logging.DEBUG.Should().BeLessThan(Sharpy.Logging.INFO);
    }

    [Fact]
    public void Constants_INFO_LessThan_WARNING()
    {
        Sharpy.Logging.INFO.Should().BeLessThan(Sharpy.Logging.WARNING);
    }

    [Fact]
    public void Constants_WARNING_LessThan_ERROR()
    {
        Sharpy.Logging.WARNING.Should().BeLessThan(Sharpy.Logging.ERROR);
    }

    [Fact]
    public void Constants_ERROR_LessThan_CRITICAL()
    {
        Sharpy.Logging.ERROR.Should().BeLessThan(Sharpy.Logging.CRITICAL);
    }

    // -------------------------------------------------------------------------
    // All log-level methods (construct Logger directly to control level)
    // -------------------------------------------------------------------------

    [Fact]
    public void Logger_Debug_AppearsWhenLevelIsDebug()
    {
        var logger = new Sharpy.Logger("lc_debug_on");
        logger.SetLevel(Sharpy.Logging.DEBUG);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Debug("dbg");
            sw.ToString().TrimEnd().Should().Be("DEBUG:lc_debug_on:dbg");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_Info_AppearsWhenLevelIsInfo()
    {
        var logger = new Sharpy.Logger("lc_info_on");
        logger.SetLevel(Sharpy.Logging.INFO);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Info("informational");
            sw.ToString().TrimEnd().Should().Be("INFO:lc_info_on:informational");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_Error_AppearsWhenLevelIsError()
    {
        var logger = new Sharpy.Logger("lc_error_on");
        logger.SetLevel(Sharpy.Logging.ERROR);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Error("something failed");
            sw.ToString().TrimEnd().Should().Be("ERROR:lc_error_on:something failed");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_Critical_AppearsWhenLevelIsCritical()
    {
        var logger = new Sharpy.Logger("lc_critical_on");
        logger.SetLevel(Sharpy.Logging.CRITICAL);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Critical("fatal error");
            sw.ToString().TrimEnd().Should().Be("CRITICAL:lc_critical_on:fatal error");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    // -------------------------------------------------------------------------
    // Level filtering
    // -------------------------------------------------------------------------

    [Fact]
    public void Logger_Debug_SuppressedWhenLevelIsWarning()
    {
        var logger = new Sharpy.Logger("lc_suppress_debug");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Debug("should be suppressed");
            sw.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_Info_SuppressedWhenLevelIsWarning()
    {
        var logger = new Sharpy.Logger("lc_suppress_info");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Info("should be suppressed");
            sw.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_Warning_PassesWhenLevelIsWarning()
    {
        var logger = new Sharpy.Logger("lc_warn_pass");
        logger.SetLevel(Sharpy.Logging.WARNING);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Warning("warn passes");
            sw.ToString().Should().Contain("warn passes");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_Error_SuppressedWhenLevelIsCritical()
    {
        var logger = new Sharpy.Logger("lc_error_suppressed");
        logger.SetLevel(Sharpy.Logging.CRITICAL);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Error("should not appear");
            sw.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Logger_SetLevel_CanLowerLevel()
    {
        // Start at CRITICAL, lower to DEBUG, verify DEBUG messages appear
        var logger = new Sharpy.Logger("lc_lower_level");
        logger.SetLevel(Sharpy.Logging.CRITICAL);
        logger.SetLevel(Sharpy.Logging.DEBUG);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            logger.Debug("should appear after lowering level");
            sw.ToString().Should().Contain("should appear after lowering level");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    // -------------------------------------------------------------------------
    // Multiple loggers — independent levels
    // -------------------------------------------------------------------------

    [Fact]
    public void MultipleLoggers_EachRespectsOwnLevel()
    {
        var warningLogger = new Sharpy.Logger("lc_multi_warn");
        warningLogger.SetLevel(Sharpy.Logging.WARNING);

        var debugLogger = new Sharpy.Logger("lc_multi_debug");
        debugLogger.SetLevel(Sharpy.Logging.DEBUG);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            warningLogger.Debug("warn_logger debug suppressed");
            debugLogger.Debug("debug_logger debug visible");

            var output = sw.ToString();
            output.Should().NotContain("warn_logger debug suppressed");
            output.Should().Contain("debug_logger debug visible");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void MultipleLoggers_DifferentNames_DifferentPrefixes()
    {
        var loggerA = new Sharpy.Logger("lc_prefix_a");
        loggerA.SetLevel(Sharpy.Logging.DEBUG);

        var loggerB = new Sharpy.Logger("lc_prefix_b");
        loggerB.SetLevel(Sharpy.Logging.DEBUG);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            loggerA.Info("msg from a");
            loggerB.Info("msg from b");

            var output = sw.ToString();
            output.Should().Contain("INFO:lc_prefix_a:msg from a");
            output.Should().Contain("INFO:lc_prefix_b:msg from b");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    // -------------------------------------------------------------------------
    // Module-level convenience functions (use unique root logger state)
    // -------------------------------------------------------------------------

    [Fact]
    public void BasicConfig_SetsRootLoggerLevel()
    {
        // Set root logger to DEBUG so module-level Debug() emits
        Sharpy.Logging.BasicConfig(Sharpy.Logging.DEBUG);

        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            Sharpy.Logging.Debug("basicconfig debug test");
            sw.ToString().Should().Contain("basicconfig debug test");
        }
        finally
        {
            Console.SetError(oldErr);
            // Restore root logger to WARNING so other tests are unaffected
            Sharpy.Logging.BasicConfig(Sharpy.Logging.WARNING);
        }
    }

    [Fact]
    public void ModuleLevel_Warning_OutputsToStderr()
    {
        // Root logger defaults to WARNING, so Warning() should always emit
        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            Sharpy.Logging.Warning("module level warning");
            sw.ToString().Should().Contain("module level warning");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void ModuleLevel_Error_OutputsToStderr()
    {
        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            Sharpy.Logging.Error("module level error");
            sw.ToString().Should().Contain("module level error");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void ModuleLevel_Critical_OutputsToStderr()
    {
        var oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            Sharpy.Logging.Critical("module level critical");
            sw.ToString().Should().Contain("module level critical");
        }
        finally
        {
            Console.SetError(oldErr);
        }
    }
}
