// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using email = global::Sharpy.EmailModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Email.EmailTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Email
    {
        [global::Sharpy.SharpyModule("email.email_tests")]
        public static partial class EmailTests
        {
        }
    }

    public static partial class Email
    {
        public partial class EmailTestsTests
        {
            [Xunit.FactAttribute]
            public void TestEmptyMessageHasNoHeaders()
            {
#line (9, 5) - (9, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (10, 5) - (10, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(msg.Keys()));
#line (11, 5) - (11, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("", msg.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestSetItemGetItemRoundTrip()
            {
#line (17, 5) - (17, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (18, 5) - (18, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("Subject", "Hello");
#line (19, 5) - (19, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Hello", msg.GetItem("Subject"));
            }

            [Xunit.FactAttribute]
            public void TestGetItemCaseInsensitive()
            {
#line (23, 5) - (23, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (24, 5) - (24, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("Subject", "Hello");
#line (25, 5) - (25, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Hello", msg.GetItem("subject"));
#line (26, 5) - (26, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Hello", msg.GetItem("SUBJECT"));
            }

            [Xunit.FactAttribute]
            public void TestGetItemMissingReturnsNone()
            {
#line (30, 5) - (30, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (31, 5) - (31, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Null(msg.GetItem("Missing"));
            }

            [Xunit.FactAttribute]
            public void TestContainsExistingHeaderReturnsTrue()
            {
#line (35, 5) - (35, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (36, 5) - (36, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("From", "a@b.com");
#line (37, 5) - (37, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.True(msg.Contains("From"));
#line (38, 5) - (38, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.True(msg.Contains("from"));
            }

            [Xunit.FactAttribute]
            public void TestContainsMissingHeaderReturnsFalse()
            {
#line (42, 5) - (42, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (43, 5) - (43, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.False(msg.Contains("Missing"));
            }

            [Xunit.FactAttribute]
            public void TestSetItemReplacesExisting()
            {
#line (47, 5) - (47, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (48, 5) - (48, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("Subject", "Old");
#line (49, 5) - (49, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("Subject", "New");
#line (50, 5) - (50, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("New", msg.GetItem("Subject"));
#line (51, 5) - (51, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(msg.Keys()));
            }

            [Xunit.FactAttribute]
            public void TestDelItemRemovesAllOccurrences()
            {
#line (55, 5) - (55, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (56, 5) - (56, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddHeader("Received", "from server1");
#line (57, 5) - (57, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddHeader("Received", "from server2");
#line (58, 5) - (58, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.DelItem("Received");
#line (59, 5) - (59, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.False(msg.Contains("Received"));
            }

            [Xunit.FactAttribute]
            public void TestAddHeaderAllowsDuplicates()
            {
#line (63, 5) - (63, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (64, 5) - (64, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddHeader("Received", "from server1");
#line (65, 5) - (65, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddHeader("Received", "from server2");
#line (66, 5) - (66, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Sharpy.List<string>? allVals = msg.GetAll("Received");
#line (67, 5) - (67, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.NotNull(allVals);
#line (68, 5) - (68, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(allVals));
            }

            [Xunit.FactAttribute]
            public void TestGetAllNonExistentReturnsNone()
            {
#line (72, 5) - (72, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (73, 5) - (73, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Null(msg.GetAll("Missing"));
            }

            [Xunit.FactAttribute]
            public void TestReplaceHeaderReplacesFirst()
            {
#line (77, 5) - (77, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (78, 5) - (78, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddHeader("Subject", "Old");
#line (79, 5) - (79, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.ReplaceHeader("Subject", "New");
#line (80, 5) - (80, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("New", msg.GetItem("Subject"));
            }

            [Xunit.FactAttribute]
            public void TestReplaceHeaderAddsIfMissing()
            {
#line (84, 5) - (84, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (85, 5) - (85, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.ReplaceHeader("Subject", "Added");
#line (86, 5) - (86, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Added", msg.GetItem("Subject"));
            }

            [Xunit.FactAttribute]
            public void TestKeysReturnsAllNames()
            {
#line (90, 5) - (90, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (91, 5) - (91, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("From", "a@b.com");
#line (92, 5) - (92, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("To", "c@d.com");
#line (93, 5) - (93, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(msg.Keys()));
            }

            [Xunit.FactAttribute]
            public void TestItemsReturnsAllPairs()
            {
#line (97, 5) - (97, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (98, 5) - (98, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("From", "a@b.com");
#line (99, 5) - (99, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("To", "c@d.com");
#line (100, 5) - (100, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var items = msg.Items();
#line (101, 5) - (101, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(items));
            }

            [Xunit.FactAttribute]
            public void TestSetContentSetsBody()
            {
#line (107, 5) - (107, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (108, 5) - (108, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("Hello world");
#line (109, 5) - (109, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Hello world", msg.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestSetContentHtmlSetsContentType()
            {
#line (113, 5) - (113, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (114, 5) - (114, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("<b>HTML</b>", "html");
#line (115, 5) - (115, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                string? ct = msg.GetItem("Content-Type");
#line (116, 5) - (116, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.NotNull(ct);
#line (117, 5) - (117, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Contains("text/html", ct);
            }

            [Xunit.FactAttribute]
            public void TestIsMultipartSimpleMessageReturnsFalse()
            {
#line (121, 5) - (121, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (122, 5) - (122, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("plain text");
#line (123, 5) - (123, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.False(msg.IsMultipart());
            }

            [Xunit.FactAttribute]
            public void TestAddAttachmentMakesMultipart()
            {
#line (127, 5) - (127, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (128, 5) - (128, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("body text");
#line (129, 5) - (129, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddAttachment(new Sharpy.Bytes(new byte[] { 1, 2, 3 }), filename: "test.bin");
#line (130, 5) - (130, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.True(msg.IsMultipart());
            }

            [Xunit.FactAttribute]
            public void TestIterAttachmentsReturnsAttachments()
            {
#line (134, 5) - (134, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (135, 5) - (135, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddAttachment(new Sharpy.Bytes(new byte[] { 1, 2, 3 }), filename: "a.bin");
#line (136, 5) - (136, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddAttachment(new Sharpy.Bytes(new byte[] { 4, 5 }), filename: "b.bin");
#line (137, 5) - (137, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(msg.IterAttachments()));
            }

            [Xunit.FactAttribute]
            public void TestGetPayloadReturnsBody()
            {
#line (141, 5) - (141, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (142, 5) - (142, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("payload");
#line (143, 5) - (143, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("payload", msg.GetPayload());
            }

            [Xunit.FactAttribute]
            public void TestGetPayloadEmptyMessageReturnsNone()
            {
#line (147, 5) - (147, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (148, 5) - (148, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Null(msg.GetPayload());
            }

            [Xunit.FactAttribute]
            public void TestAsStringSimpleMessageProducesRfc5322()
            {
#line (154, 5) - (154, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (155, 5) - (155, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("Subject", "Test");
#line (156, 5) - (156, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("From", "sender@example.com");
#line (157, 5) - (157, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("Hello");
#line (158, 5) - (158, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                string result = msg.AsString();
#line (159, 5) - (159, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Contains("Subject: Test\r\n", result);
#line (160, 5) - (160, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Contains("From: sender@example.com\r\n", result);
#line (161, 5) - (161, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Contains("Hello", result);
            }

            [Xunit.FactAttribute]
            public void TestAsStringMultipartMessageHasBoundaries()
            {
#line (165, 5) - (165, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (166, 5) - (166, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("body");
#line (167, 5) - (167, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.AddAttachment(new Sharpy.Bytes(new byte[] { 1, 2, 3 }), filename: "test.bin");
#line (168, 5) - (168, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                string result = msg.AsString();
#line (169, 5) - (169, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Contains("--", result);
#line (170, 5) - (170, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Contains("Content-Transfer-Encoding: base64", result);
            }

            [Xunit.FactAttribute]
            public void TestAsBytesReturnsUtf8()
            {
#line (174, 5) - (174, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = new global::Sharpy.EmailMessage();
#line (175, 5) - (175, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetItem("Subject", "Test");
#line (176, 5) - (176, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                msg.SetContent("Hello");
#line (177, 5) - (177, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Sharpy.Bytes b = msg.AsBytes();
#line (178, 5) - (178, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(b) > 0);
            }

            [Xunit.FactAttribute]
            public void TestMessageFromStringParsesHeadersAndBody()
            {
#line (184, 5) - (184, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = email.MessageFromString("Subject: test\nFrom: a@b.com\n\nBody text");
#line (185, 5) - (185, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("test", msg.GetItem("Subject"));
#line (186, 5) - (186, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("a@b.com", msg.GetItem("From"));
#line (187, 5) - (187, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Body text", msg.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestMessageFromStringContinuationLines()
            {
#line (191, 5) - (191, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = email.MessageFromString("Subject: long\n subject\n\nBody");
#line (192, 5) - (192, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("long subject", msg.GetItem("Subject"));
            }

            [Xunit.FactAttribute]
            public void TestMessageFromStringEmptyStringReturnsEmpty()
            {
#line (196, 5) - (196, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = email.MessageFromString("");
#line (197, 5) - (197, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(msg.Keys()));
#line (198, 5) - (198, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("", msg.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestMessageFromStringHeadersOnly()
            {
#line (202, 5) - (202, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = email.MessageFromString("Subject: test\nFrom: a@b.com");
#line (203, 5) - (203, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("test", msg.GetItem("Subject"));
#line (204, 5) - (204, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("", msg.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestMessageFromStringCrlfLineEndings()
            {
#line (208, 5) - (208, 82) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = email.MessageFromString("Subject: test\r\nFrom: a@b.com\r\n\r\nBody");
#line (209, 5) - (209, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("test", msg.GetItem("Subject"));
#line (210, 5) - (210, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Body", msg.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestMessageFromBytesParsesUtf8()
            {
#line (214, 5) - (214, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Sharpy.Bytes data = new Sharpy.Bytes(new byte[] { 83, 117, 98, 106, 101, 99, 116, 58, 32, 116, 101, 115, 116, 10, 10, 72, 101, 108, 108, 111 });
#line (215, 5) - (215, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var msg = email.MessageFromBytes(data);
#line (216, 5) - (216, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("test", msg.GetItem("Subject"));
#line (217, 5) - (217, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Hello", msg.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestRoundtripCreateAndParse()
            {
#line (221, 5) - (221, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var original = new global::Sharpy.EmailMessage();
#line (222, 5) - (222, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                original.SetItem("Subject", "Round Trip");
#line (223, 5) - (223, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                original.SetItem("From", "sender@example.com");
#line (224, 5) - (224, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                original.SetContent("Test body");
#line (225, 5) - (225, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var parsed = email.MessageFromString(original.AsString());
#line (226, 5) - (226, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("Round Trip", parsed.GetItem("Subject"));
#line (227, 5) - (227, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Equal("sender@example.com", parsed.GetItem("From"));
#line (228, 5) - (228, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.Contains("Test body", parsed.GetContent());
            }

            [Xunit.FactAttribute]
            public void TestMessageErrorIsException()
            {
#line (234, 5) - (234, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var err = new global::Sharpy.MessageError("test");
#line (235, 5) - (235, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.IsAssignableFrom<Exception>(err);
            }

            [Xunit.FactAttribute]
            public void TestMessageParseErrorInheritsFromMessageError()
            {
#line (239, 5) - (239, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var err = new global::Sharpy.MessageParseError("test");
#line (240, 5) - (240, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.True(err is global::Sharpy.MessageError);
            }

            [Xunit.FactAttribute]
            public void TestHeaderParseErrorInheritsFromMessageError()
            {
#line (244, 5) - (244, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                var err = new global::Sharpy.HeaderParseError("test");
#line (245, 5) - (245, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/email/email_tests.spy"
                Xunit.Assert.True(err is global::Sharpy.MessageError);
            }
        }
    }
}
