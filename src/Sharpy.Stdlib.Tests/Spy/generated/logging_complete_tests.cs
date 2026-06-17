// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using logging = global::Sharpy.Logging;
using static global::Sharpy.Unittest;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Logging.LoggingCompleteTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Logging
    {
        [global::Sharpy.SharpyModule("logging.logging_complete_tests")]
        public static partial class LoggingCompleteTests
        {
        }
    }

    public static partial class Logging
    {
        public partial class LoggingCompleteTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDebugLessThanInfo()
            {
#line (20, 5) - (20, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.DEBUG < logging.INFO);
            }

            [Xunit.FactAttribute]
            public void TestInfoLessThanWarning()
            {
#line (25, 5) - (25, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.INFO < logging.WARNING);
            }

            [Xunit.FactAttribute]
            public void TestWarningLessThanError()
            {
#line (30, 5) - (30, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.WARNING < logging.ERROR);
            }

            [Xunit.FactAttribute]
            public void TestErrorLessThanCritical()
            {
#line (35, 5) - (35, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                Xunit.Assert.True(logging.ERROR < logging.CRITICAL);
            }

            [Xunit.FactAttribute]
            public void TestDebugAppearsWhenLevelIsDebug()
            {
#line (42, 5) - (42, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_debug_on");
#line (43, 5) - (43, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.DEBUG);
#line (44, 5) - (49, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (45, 9) - (45, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Debug("dbg");
#line (46, 9) - (46, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Equal("DEBUG:lc_debug_on:dbg", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestInfoAppearsWhenLevelIsInfo()
            {
#line (51, 5) - (51, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_info_on");
#line (52, 5) - (52, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.INFO);
#line (53, 5) - (58, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (54, 9) - (54, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Info("informational");
#line (55, 9) - (55, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Equal("INFO:lc_info_on:informational", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestErrorAppearsWhenLevelIsError()
            {
#line (60, 5) - (60, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_error_on");
#line (61, 5) - (61, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.ERROR);
#line (62, 5) - (67, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (63, 9) - (63, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Error("something failed");
#line (64, 9) - (64, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Equal("ERROR:lc_error_on:something failed", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestCriticalAppearsWhenLevelIsCritical()
            {
#line (69, 5) - (69, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_critical_on");
#line (70, 5) - (70, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.CRITICAL);
#line (71, 5) - (78, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (72, 9) - (72, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Critical("fatal error");
#line (73, 9) - (73, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Equal("CRITICAL:lc_critical_on:fatal error", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestDebugSuppressedWhenLevelIsWarning()
            {
#line (80, 5) - (80, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_suppress_debug");
#line (81, 5) - (81, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (82, 5) - (87, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (83, 9) - (83, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Debug("should be suppressed");
#line (84, 9) - (84, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Equal("", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestInfoSuppressedWhenLevelIsWarning()
            {
#line (89, 5) - (89, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_suppress_info");
#line (90, 5) - (90, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (91, 5) - (96, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (92, 9) - (92, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Info("should be suppressed");
#line (93, 9) - (93, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Equal("", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestWarningPassesWhenLevelIsWarning()
            {
#line (98, 5) - (98, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_warn_pass");
#line (99, 5) - (99, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (100, 5) - (105, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (101, 9) - (101, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Warning("warn passes");
#line (102, 9) - (102, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("warn passes", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestErrorSuppressedWhenLevelIsCritical()
            {
#line (107, 5) - (107, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_error_suppressed");
#line (108, 5) - (108, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.CRITICAL);
#line (109, 5) - (114, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (110, 9) - (110, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Error("should not appear");
#line (111, 9) - (111, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Equal("", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestSetLevelCanLowerLevel()
            {
#line (117, 5) - (117, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var logger = new global::Sharpy.Logger("lc_lower_level");
#line (118, 5) - (118, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.CRITICAL);
#line (119, 5) - (119, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logger.SetLevel(logging.DEBUG);
#line (120, 5) - (127, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (121, 9) - (121, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logger.Debug("should appear after lowering level");
#line (122, 9) - (122, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("should appear after lowering level", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestMultipleLoggersEachRespectsOwnLevel()
            {
#line (129, 5) - (129, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var warningLogger = new global::Sharpy.Logger("lc_multi_warn");
#line (130, 5) - (130, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                warningLogger.SetLevel(logging.WARNING);
#line (132, 5) - (132, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var debugLogger = new global::Sharpy.Logger("lc_multi_debug");
#line (133, 5) - (133, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                debugLogger.SetLevel(logging.DEBUG);
#line (135, 5) - (144, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (136, 9) - (136, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    warningLogger.Debug("warn_logger debug suppressed");
#line (137, 9) - (137, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    debugLogger.Debug("debug_logger debug visible");
#line (139, 9) - (139, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    var output = err.Getvalue();
#line (140, 9) - (140, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.DoesNotContain("warn_logger debug suppressed", output);
#line (141, 9) - (141, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("debug_logger debug visible", output);
                }
            }

            [Xunit.FactAttribute]
            public void TestMultipleLoggersDifferentNamesDifferentPrefixes()
            {
#line (146, 5) - (146, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var loggerA = new global::Sharpy.Logger("lc_prefix_a");
#line (147, 5) - (147, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                loggerA.SetLevel(logging.DEBUG);
#line (149, 5) - (149, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                var loggerB = new global::Sharpy.Logger("lc_prefix_b");
#line (150, 5) - (150, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                loggerB.SetLevel(logging.DEBUG);
#line (152, 5) - (163, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (153, 9) - (153, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    loggerA.Info("msg from a");
#line (154, 9) - (154, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    loggerB.Info("msg from b");
#line (156, 9) - (156, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    var output = err.Getvalue();
#line (157, 9) - (157, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("INFO:lc_prefix_a:msg from a", output);
#line (158, 9) - (158, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("INFO:lc_prefix_b:msg from b", output);
                }
            }

            [Xunit.FactAttribute]
            public void TestBasicConfigSetsRootLoggerLevel()
            {
#line (166, 5) - (166, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logging.BasicConfig(logging.DEBUG);
#line (167, 5) - (171, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (168, 9) - (168, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logging.Debug("basicconfig debug test");
#line (169, 9) - (169, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("basicconfig debug test", err.Getvalue());
                }

#line (171, 5) - (171, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                logging.BasicConfig(logging.WARNING);
            }

            [Xunit.FactAttribute]
            public void TestModuleLevelWarningOutputsToStderr()
            {
#line (177, 5) - (182, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (178, 9) - (178, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logging.Warning("module level warning");
#line (179, 9) - (179, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("module level warning", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestModuleLevelErrorOutputsToStderr()
            {
#line (184, 5) - (189, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (185, 9) - (185, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logging.Error("module level error");
#line (186, 9) - (186, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("module level error", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestModuleLevelCriticalOutputsToStderr()
            {
#line (191, 5) - (194, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                using (var err = CapturedStderr())
                {
#line (192, 9) - (192, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    logging.Critical("module level critical");
#line (193, 9) - (193, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_complete_tests.spy"
                    Xunit.Assert.Contains("module level critical", err.Getvalue());
                }
            }
        }
    }
}
