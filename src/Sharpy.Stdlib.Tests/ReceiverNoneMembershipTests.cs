using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// plan-4e2867 D6 (#2635): a Stdlib receiver's public single-argument <c>Contains</c> — the method
/// the compiler binds for <c>x in receiver</c> — answers a <c>None</c> needle as CPython does,
/// never with a CLR exception. A <c>T | None</c> needle reaches the receiver as <c>null</c> at
/// run time, so these cells call <c>Contains(null!)</c> directly.
///
/// Each cell carries CPython 3.15.2's answer for the same program (verified with
/// <c>python3.15 -c</c>). Only the exception TYPE is compared with CPython; the message is
/// DEVIATION by default and is asserted here only as the wording this implementation chose.
/// Every null cell has a non-null member and non-member control on the same receiver.
/// </summary>
public class ReceiverNoneMembershipTests
{
    // ---- Counter: `None in Counter(["a", "b", "a"])` -> False ----

    [Fact]
    public void Counter_NullKey_IsNotAMember()
    {
        var counter = new Counter<string>(new[] { "a", "b", "a" });

        Assert.False(counter.Contains(null!));
    }

    [Fact]
    public void Counter_EmptyCounter_NullKey_IsNotAMember()
    {
        // CPython: None in Counter() -> False
        var counter = new Counter<string>();

        Assert.False(counter.Contains(null!));
    }

    [Fact]
    public void Counter_NonNullKeys_AreUnchanged()
    {
        // CPython: "a" in c -> True; "z" in c -> False
        var counter = new Counter<string>(new[] { "a", "b", "a" });

        Assert.True(counter.Contains("a"));
        Assert.False(counter.Contains("z"));
    }

    [Fact]
    public void Counter_ValueTypedKeys_AreUnchanged()
    {
        // The null arm must not disturb a value-typed key, where `key is null` is never true.
        // CPython: 0 in Counter([0, 1]) -> True; 7 in Counter([0, 1]) -> False
        var counter = new Counter<int>(new[] { 0, 1 });

        Assert.True(counter.Contains(0));
        Assert.False(counter.Contains(7));
    }

    // ---- IPv4Network: `None in IPv4Network("192.168.1.0/24")` -> AttributeError ----

    [Fact]
    public void IPv4Network_NullAddress_RaisesAttributeError()
    {
        var network = new IPv4Network("192.168.1.0/24");

        var ex = Assert.Throws<AttributeError>(() => network.Contains(null!));
        Assert.Equal("'NoneType' object has no attribute 'version'", ex.Message);
    }

    [Fact]
    public void IPv4Network_NonNullAddresses_AreUnchanged()
    {
        // CPython: IPv4Address("192.168.1.7") in n -> True; IPv4Address("10.0.0.1") in n -> False
        var network = new IPv4Network("192.168.1.0/24");

        Assert.True(network.Contains(new IPv4Address("192.168.1.7")));
        Assert.False(network.Contains(new IPv4Address("10.0.0.1")));
    }

    // ---- IPv6Network: `None in IPv6Network("2001:db8::/32")` -> AttributeError ----

    [Fact]
    public void IPv6Network_NullAddress_RaisesAttributeError()
    {
        var network = new IPv6Network("2001:db8::/32");

        var ex = Assert.Throws<AttributeError>(() => network.Contains(null!));
        Assert.Equal("'NoneType' object has no attribute 'version'", ex.Message);
    }

    [Fact]
    public void IPv6Network_NonNullAddresses_AreUnchanged()
    {
        // CPython: IPv6Address("2001:db8::1") in n -> True; IPv6Address("fe80::1") in n -> False
        var network = new IPv6Network("2001:db8::/32");

        Assert.True(network.Contains(new IPv6Address("2001:db8::1")));
        Assert.False(network.Contains(new IPv6Address("fe80::1")));
    }

    // ---- EmailMessage: `None in EmailMessage()` -> AttributeError ----

    [Fact]
    public void EmailMessage_NullName_RaisesAttributeError()
    {
        var message = new EmailMessage();
        message.SetItem("Subject", "hello");

        var ex = Assert.Throws<AttributeError>(() => message.Contains(null!));
        Assert.Equal("'NoneType' object has no attribute 'lower'", ex.Message);
    }

    [Fact]
    public void EmailMessage_NoHeaders_NullName_RaisesAttributeError()
    {
        // CPython raises before it looks at any header, so an empty message raises too.
        var message = new EmailMessage();

        Assert.Throws<AttributeError>(() => message.Contains(null!));
    }

    [Fact]
    public void EmailMessage_NonNullNames_AreUnchanged()
    {
        // CPython: "subject" in m -> True (case-insensitive); "To" in m -> False
        var message = new EmailMessage();
        message.SetItem("Subject", "hello");

        Assert.True(message.Contains("Subject"));
        Assert.True(message.Contains("subject"));
        Assert.False(message.Contains("To"));
    }
}
