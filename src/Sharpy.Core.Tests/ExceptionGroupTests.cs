using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy;

namespace Sharpy.Core.Tests;

public class ExceptionGroupTests
{
    [Fact]
    public void Constructor_SetsMessageAndExceptions()
    {
        var excs = new List<Exception> { new InvalidOperationException("a"), new ArgumentException("b") };
        var eg = new ExceptionGroup("test", excs);

        eg.Message.Should().Be("test");
        eg.Exceptions.Should().HaveCount(2);
        eg.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_AcceptsIEnumerable()
    {
        IEnumerable<Exception> excs = new[] { new InvalidOperationException("a") };
        var eg = new ExceptionGroup("msg", excs);

        eg.Message.Should().Be("msg");
        eg.Exceptions.Should().HaveCount(1);
    }

    [Fact]
    public void Exceptions_DelegatesToInnerExceptions()
    {
        var exc = new ArgumentException("x");
        var eg = new ExceptionGroup("test", new List<Exception> { exc });

        eg.Exceptions[0].Should().BeSameAs(exc);
    }

    [Fact]
    public void Subgroup_MatchingSome_ReturnsFilteredGroup()
    {
        var v1 = new ArgumentException("v1");
        var t1 = new InvalidOperationException("t1");
        var v2 = new ArgumentException("v2");
        var eg = new ExceptionGroup("test", new List<Exception> { v1, t1, v2 });

        var sub = eg.Subgroup(e => e is ArgumentException);

        sub.Should().NotBeNull();
        sub.Message.Should().Be("test");
        sub.Exceptions.Should().HaveCount(2);
        sub.Exceptions.Should().Contain(v1);
        sub.Exceptions.Should().Contain(v2);
    }

    [Fact]
    public void Subgroup_MatchingAll_ReturnsAllExceptions()
    {
        var v1 = new ArgumentException("v1");
        var v2 = new ArgumentException("v2");
        var eg = new ExceptionGroup("test", new List<Exception> { v1, v2 });

        var sub = eg.Subgroup(e => e is ArgumentException);

        sub.Should().NotBeNull();
        sub.Exceptions.Should().HaveCount(2);
    }

    [Fact]
    public void Subgroup_MatchingNone_ReturnsNull()
    {
        var eg = new ExceptionGroup("test", new List<Exception>
        {
            new ArgumentException("a"),
            new InvalidOperationException("b")
        });

        var sub = eg.Subgroup(e => e is NullReferenceException);

        sub.Should().BeNull();
    }

    [Fact]
    public void Subgroup_NestedExceptionGroup_RecursivelyFilters()
    {
        var inner = new ExceptionGroup("inner", new List<Exception> { new ArgumentException("k") });
        var outer = new ExceptionGroup("outer", new List<Exception>
        {
            new InvalidOperationException("v"),
            inner
        });

        var sub = outer.Subgroup(e => e is ArgumentException);

        sub.Should().NotBeNull();
        sub.Message.Should().Be("outer");
        sub.Exceptions.Should().HaveCount(1);
        sub.Exceptions[0].Should().BeOfType<ExceptionGroup>();
        var nestedResult = (ExceptionGroup)sub.Exceptions[0];
        nestedResult.Exceptions.Should().HaveCount(1);
        nestedResult.Exceptions[0].Should().BeOfType<ArgumentException>();
    }

    [Fact]
    public void Split_PartitionsCorrectly()
    {
        var v = new ArgumentException("v");
        var t = new InvalidOperationException("t");
        var eg = new ExceptionGroup("test", new List<Exception> { v, t });

        var (match, rest) = eg.Split(e => e is ArgumentException);

        match.Should().NotBeNull();
        match.Exceptions.Should().HaveCount(1);
        match.Exceptions[0].Should().BeSameAs(v);

        rest.Should().NotBeNull();
        rest.Exceptions.Should().HaveCount(1);
        rest.Exceptions[0].Should().BeSameAs(t);
    }

    [Fact]
    public void Split_AllMatch_RestIsNull()
    {
        var eg = new ExceptionGroup("test", new List<Exception>
        {
            new ArgumentException("a"),
            new ArgumentException("b")
        });

        var (match, rest) = eg.Split(e => e is ArgumentException);

        match.Should().NotBeNull();
        match.Exceptions.Should().HaveCount(2);
        rest.Should().BeNull();
    }

    [Fact]
    public void Split_NoneMatch_MatchIsNull()
    {
        var eg = new ExceptionGroup("test", new List<Exception>
        {
            new ArgumentException("a"),
            new InvalidOperationException("b")
        });

        var (match, rest) = eg.Split(e => e is NullReferenceException);

        match.Should().BeNull();
        rest.Should().NotBeNull();
        rest.Exceptions.Should().HaveCount(2);
    }

    [Fact]
    public void Split_NestedExceptionGroup_RecursivelyPartitions()
    {
        var inner = new ExceptionGroup("inner", new List<Exception>
        {
            new ArgumentException("a"),
            new InvalidOperationException("t")
        });
        var outer = new ExceptionGroup("outer", new List<Exception>
        {
            new ArgumentException("v"),
            inner
        });

        var (match, rest) = outer.Split(e => e is ArgumentException);

        match.Should().NotBeNull();
        match.Exceptions.Should().HaveCount(2);
        match.Exceptions[0].Should().BeOfType<ArgumentException>();
        match.Exceptions[1].Should().BeOfType<ExceptionGroup>();

        rest.Should().NotBeNull();
        rest.Exceptions.Should().HaveCount(1);
        rest.Exceptions[0].Should().BeOfType<ExceptionGroup>();
    }

    [Fact]
    public void Derive_CreatesNewGroupWithSameMessage()
    {
        var eg = new ExceptionGroup("original", new List<Exception> { new ArgumentException("a") });
        var newExcs = new List<Exception> { new InvalidOperationException("b") };

        var derived = eg.Derive(newExcs);

        derived.Message.Should().Be("original");
        derived.Exceptions.Should().HaveCount(1);
        derived.Exceptions[0].Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Derive_PreservesSubclassType()
    {
        var eg = new CustomExceptionGroup("test", new List<Exception> { new ArgumentException("a") });
        var derived = eg.Derive(new List<Exception> { new InvalidOperationException("b") });

        derived.Should().BeOfType<CustomExceptionGroup>();
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var eg = new ExceptionGroup("test", new List<Exception>
        {
            new ArgumentException("v"),
            new InvalidOperationException("t")
        });

        var str = eg.ToString();

        str.Should().Contain("ExceptionGroup");
        str.Should().Contain("test");
        str.Should().Contain("ArgumentException");
        str.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void IsAggregateException()
    {
        var eg = new ExceptionGroup("test", new List<Exception> { new ArgumentException("a") });

        eg.Should().BeAssignableTo<AggregateException>();
    }

    [Fact]
    public void PreservesNesting_DoesNotFlatten()
    {
        var inner = new ExceptionGroup("inner", new List<Exception> { new ArgumentException("a") });
        var outer = new ExceptionGroup("outer", new List<Exception> { inner });

        outer.Exceptions.Should().HaveCount(1);
        outer.Exceptions[0].Should().BeOfType<ExceptionGroup>();
    }

    private class CustomExceptionGroup : ExceptionGroup
    {
        public CustomExceptionGroup(string message, List<Exception> exceptions)
            : base(message, exceptions) { }

        public override ExceptionGroup Derive(IEnumerable<Exception> exceptions)
        {
            return new CustomExceptionGroup(Message, new List<Exception>(exceptions));
        }
    }
}
