using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>The interop attributes discovery reads for a module imported by reference (plan-775c50 D10).</summary>
public class SharpyStaticStorageAttributeTests
{
    [Fact]
    public void StaticStorage_CarriesItsKind_AndPlainIsTheDefault()
    {
        new SharpyStaticStorageAttribute(StaticStorageKind.Guarded).Kind.Should().Be(StaticStorageKind.Guarded);
        default(StaticStorageKind).Should().Be(StaticStorageKind.Plain);
    }

    [Fact]
    public void LoadEntry_CarriesItsSweepFact()
    {
        new SharpyLoadEntryAttribute(true).HasSweep.Should().BeTrue();
        new SharpyLoadEntryAttribute(false).HasSweep.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(SharpyStaticStorageAttribute), AttributeTargets.Field | AttributeTargets.Property)]
    [InlineData(typeof(SharpyLoadEntryAttribute), AttributeTargets.Method)]
    public void Attributes_TargetTheMembersCodegenMarks_OncePerMember(Type attribute, AttributeTargets targets)
    {
        var usage = attribute.GetCustomAttribute<AttributeUsageAttribute>()!;
        usage.ValidOn.Should().Be(targets);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }
}
