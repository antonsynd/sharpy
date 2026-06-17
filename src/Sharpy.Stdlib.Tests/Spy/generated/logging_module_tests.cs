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
using static Sharpy.Stdlib.Tests.Spy.Logging.LoggingModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Logging
    {
        [global::Sharpy.SharpyModule("logging.logging_module_tests")]
        public static partial class LoggingModuleTests
        {
        }
    }

    public static partial class Logging
    {
        public partial class LoggingModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestConstantsHaveCorrectValues()
            {
#line (15, 5) - (15, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(10, logging.DEBUG);
#line (16, 5) - (16, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(20, logging.INFO);
#line (17, 5) - (17, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(30, logging.WARNING);
#line (18, 5) - (18, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(40, logging.ERROR);
#line (19, 5) - (19, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                Xunit.Assert.Equal(50, logging.CRITICAL);
            }

            [Xunit.FactAttribute]
            public void TestWarningOutputsToStderr()
            {
#line (26, 5) - (26, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_output");
#line (27, 5) - (27, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (28, 5) - (33, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (29, 9) - (29, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("test message");
#line (30, 9) - (30, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Equal("WARNING:test_output:test message", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestLevelFilteringBelowLevel()
            {
#line (35, 5) - (35, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_filter");
#line (36, 5) - (36, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.ERROR);
#line (37, 5) - (42, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (38, 9) - (38, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("should not appear");
#line (39, 9) - (39, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Equal("", err.Getvalue());
                }
            }

            [Xunit.FactAttribute]
            public void TestLevelFilteringAtLevel()
            {
#line (44, 5) - (44, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_at");
#line (45, 5) - (45, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (46, 5) - (51, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (47, 9) - (47, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("at level");
#line (48, 9) - (48, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Equal("WARNING:test_at:at level", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestOutputFormat()
            {
#line (53, 5) - (53, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("myapp");
#line (54, 5) - (54, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.DEBUG);
#line (55, 5) - (68, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (56, 9) - (56, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Debug("debug msg");
#line (57, 9) - (57, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Info("info msg");
#line (58, 9) - (58, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Error("error msg");
#line (59, 9) - (59, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Critical("critical msg");
#line (61, 9) - (61, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    var output = err.Getvalue();
#line (62, 9) - (62, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("DEBUG:myapp:debug msg", output);
#line (63, 9) - (63, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("INFO:myapp:info msg", output);
#line (64, 9) - (64, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("ERROR:myapp:error msg", output);
#line (65, 9) - (65, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("CRITICAL:myapp:critical msg", output);
                }
            }

            [Xunit.FactAttribute]
            public void TestDefaultLevelIsWarning()
            {
#line (70, 5) - (70, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_default");
#line (71, 5) - (80, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (72, 9) - (72, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Info("should not appear");
#line (73, 9) - (73, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("should appear");
#line (75, 9) - (75, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    var output = err.Getvalue();
#line (76, 9) - (76, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.DoesNotContain("INFO", output);
#line (77, 9) - (77, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("WARNING", output);
                }
            }

            [Xunit.FactAttribute]
            public void TestHigherSeverityPassesLowerThreshold()
            {
#line (83, 5) - (83, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_higher_sev");
#line (84, 5) - (84, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (85, 5) - (94, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (86, 9) - (86, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Error("err msg");
#line (87, 9) - (87, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Critical("crit msg");
#line (89, 9) - (89, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    var output = err.Getvalue();
#line (90, 9) - (90, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("ERROR:test_higher_sev:err msg", output);
#line (91, 9) - (91, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("CRITICAL:test_higher_sev:crit msg", output);
                }
            }

            [Xunit.FactAttribute]
            public void TestAllLevelsAtOrAboveInfoEmit()
            {
#line (96, 5) - (96, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_all_above_info");
#line (97, 5) - (97, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.INFO);
#line (98, 5) - (113, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (99, 9) - (99, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Debug("dbg suppressed");
#line (100, 9) - (100, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Info("info shown");
#line (101, 9) - (101, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("warn shown");
#line (102, 9) - (102, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Error("error shown");
#line (103, 9) - (103, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Critical("critical shown");
#line (105, 9) - (105, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    var output = err.Getvalue();
#line (106, 9) - (106, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.DoesNotContain("DEBUG", output);
#line (107, 9) - (107, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("INFO:test_all_above_info:info shown", output);
#line (108, 9) - (108, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("WARNING:test_all_above_info:warn shown", output);
#line (109, 9) - (109, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("ERROR:test_all_above_info:error shown", output);
#line (110, 9) - (110, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("CRITICAL:test_all_above_info:critical shown", output);
                }
            }

            [Xunit.FactAttribute]
            public void TestEmptyMessageEmitsPrefixOnly()
            {
#line (115, 5) - (115, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_empty_msg");
#line (116, 5) - (116, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (117, 5) - (122, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (118, 9) - (118, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("");
#line (119, 9) - (119, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Equal("WARNING:test_empty_msg:", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestMessageWithColonsPreservedVerbatim()
            {
#line (125, 5) - (125, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_colons");
#line (126, 5) - (126, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.WARNING);
#line (127, 5) - (132, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (128, 9) - (128, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("key:value:pair");
#line (129, 9) - (129, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Equal("WARNING:test_colons:key:value:pair", err.Getvalue().Rstrip());
                }
            }

            [Xunit.FactAttribute]
            public void TestMultipleMessagesAppearInOrder()
            {
#line (134, 5) - (134, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_order");
#line (135, 5) - (135, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.DEBUG);
#line (136, 5) - (147, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (137, 9) - (137, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Debug("first");
#line (138, 9) - (138, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Info("second");
#line (139, 9) - (139, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("third");
#line (143, 9) - (143, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    var output = err.Getvalue().Rstrip();
#line (144, 9) - (144, 101) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Equal("DEBUG:test_order:first\nINFO:test_order:second\nWARNING:test_order:third", output);
                }
            }

            [Xunit.FactAttribute]
            public void TestSetLevelRaiseLevelSuppressesLowerSeverity()
            {
#line (150, 5) - (150, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                var logger = new global::Sharpy.Logger("test_raise_level");
#line (151, 5) - (151, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.DEBUG);
#line (152, 5) - (152, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                logger.SetLevel(logging.ERROR);
#line (153, 5) - (160, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                using (var err = CapturedStderr())
                {
#line (154, 9) - (154, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Warning("should be suppressed after raise");
#line (155, 9) - (155, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    logger.Error("should still appear");
#line (157, 9) - (157, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    var output = err.Getvalue();
#line (158, 9) - (158, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.DoesNotContain("should be suppressed after raise", output);
#line (159, 9) - (159, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/logging/logging_module_tests.spy"
                    Xunit.Assert.Contains("should still appear", output);
                }
            }
        }
    }
}
