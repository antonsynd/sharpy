using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Equality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;
        Object objectRef = l;
        object objRef = l;

        // When/then
        (l == listRef).Should().BeTrue();
        (l == objectRef).Should().BeTrue();
        (l == objRef).Should().BeTrue();
        l.Equals(listRef).Should().BeTrue();
        l.Equals(objectRef).Should().BeTrue();
        l.Equals(objRef).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Same_Object_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;
        Object objectRef = l;
        object objRef = l;

        // When/then
        (listRef == l).Should().BeTrue();
        (objectRef == l).Should().BeTrue();
        (objRef == l).Should().BeTrue();
        listRef.Equals(l).Should().BeTrue();
        objectRef.Equals(l).Should().BeTrue();
        objRef.Equals(l).Should().BeTrue();
    }

    [Fact]
    public void List_Equality_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        (l == (List<int>)null).Should().BeFalse();
        (l == (Object)null).Should().BeFalse();
        (l == (object)null).Should().BeFalse();
        l.Equals((List<int>)null).Should().BeFalse();
        l.Equals((Object)null).Should().BeFalse();
        l.Equals((object)null).Should().BeFalse();
    }

    [Fact]
    public void List_Equality_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((List<int>)null == l).Should().BeFalse();
        ((Object)null == l).Should().BeFalse();
        ((object)null == l).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Same_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;
        Object objectRef = l;
        object objRef = l;

        // When/then
        (l != listRef).Should().BeFalse();
        (l != objectRef).Should().BeFalse();
        (l != objRef).Should().BeFalse();
        (!l.Equals(listRef)).Should().BeFalse();
        (!l.Equals(objectRef)).Should().BeFalse();
        (!l.Equals(objRef)).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Same_Object_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var listRef = l;
        Object objectRef = l;
        object objRef = l;

        (listRef != l).Should().BeFalse();
        (objectRef != l).Should().BeFalse();
        (objRef != l).Should().BeFalse();
        (!listRef.Equals(l)).Should().BeFalse();
        (!objectRef.Equals(l)).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Null()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        (l != (List<int>)null).Should().BeTrue();
        (l != (Object)null).Should().BeTrue();
        (l != (object)null).Should().BeTrue();
        (!l.Equals((List<int>)null)).Should().BeTrue();
        (!l.Equals((Object)null)).Should().BeTrue();
        (!l.Equals((object)null)).Should().BeTrue();
    }

    [Fact]
    public void List_Inequality_Null_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        ((List<int>)null != l).Should().BeTrue();
        ((Object)null != l).Should().BeTrue();
        ((object)null != l).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Different_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (l == m).Should().BeFalse();
        (l == objectRef).Should().BeFalse();
        (l == objRef).Should().BeFalse();
        l.Equals(m).Should().BeFalse();
        l.Equals(objectRef).Should().BeFalse();
        l.Equals(objRef).Should().BeFalse();

        // When
        m.Pop();

        // Then
        (l == m).Should().BeTrue();
        (l == objectRef).Should().BeTrue();
        (l == objRef).Should().BeTrue();
        l.Equals(m).Should().BeTrue();
        l.Equals(objectRef).Should().BeTrue();
        l.Equals(objRef).Should().BeTrue();
    }

    [Fact]
    public void List_Native_Equality_Different_Object_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m == l).Should().BeFalse();
        (objectRef == l).Should().BeFalse();
        (objRef == l).Should().BeFalse();
        m.Equals(l).Should().BeFalse();
        objectRef.Equals(l).Should().BeFalse();
        objRef.Equals(l).Should().BeFalse();

        // When
        m.Pop();

        // Then
        (m == l).Should().BeTrue();
        (objectRef == l).Should().BeTrue();
        (objRef == l).Should().BeTrue();
        m.Equals(l).Should().BeTrue();
        objectRef.Equals(l).Should().BeTrue();
        l.Equals(objRef).Should().BeTrue();
    }

    [Fact]
    public void List_Inequality_Different_Object()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (l != m).Should().BeTrue();
        (l != objectRef).Should().BeTrue();
        (l != objRef).Should().BeTrue();
        (!l.Equals(m)).Should().BeTrue();
        (!l.Equals(objectRef)).Should().BeTrue();
        (!l.Equals(objRef)).Should().BeTrue();

        // When
        m.Pop();

        // Then
        (l != m).Should().BeFalse();
        (l != objectRef).Should().BeFalse();
        (l != objRef).Should().BeFalse();
        (!l.Equals(m)).Should().BeFalse();
        (!l.Equals(objectRef)).Should().BeFalse();
        (!l.Equals(objRef)).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Different_Object_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> m = [1, 3, 5, 7, 9];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m != l).Should().BeTrue();
        (objectRef != l).Should().BeTrue();
        (objRef != l).Should().BeTrue();
        (!m.Equals(l)).Should().BeTrue();
        (!objectRef.Equals(l)).Should().BeTrue();

        // When
        m.Pop();

        // Then
        (m != l).Should().BeFalse();
        (objectRef != l).Should().BeFalse();
        (objRef != l).Should().BeFalse();
        (!m.Equals(l)).Should().BeFalse();
        (!objectRef.Equals(l)).Should().BeFalse();
    }

    [Fact]
    public void List_Equality_Different_Type()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (l == m).Should().BeFalse();
        (l == objectRef).Should().BeFalse();
        (l == objRef).Should().BeFalse();
        l.Equals(m).Should().BeFalse();
        l.Equals(objectRef).Should().BeFalse();
        l.Equals(objRef).Should().BeFalse();
    }

    [Fact]
    public void List_Equality_Different_Type_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m == l).Should().BeFalse();
        (objectRef == l).Should().BeFalse();
        (objRef == l).Should().BeFalse();
        m.Equals(l).Should().BeFalse();
        objectRef.Equals(l).Should().BeFalse();
        objRef.Equals(l).Should().BeFalse();
    }

    [Fact]
    public void List_Inequality_Different_Type()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (l != m).Should().BeTrue();
        (l != objectRef).Should().BeTrue();
        (l != objRef).Should().BeTrue();
        (!l.Equals(m)).Should().BeTrue();
        (!l.Equals(objectRef)).Should().BeTrue();
        (!l.Equals(objRef)).Should().BeTrue();
    }

    [Fact]
    public void List_Inequality_Different_Type_Reverse()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<double> m = [1.0, 3.0, 5.0, 7.0];
        Object objectRef = m;
        object objRef = m;

        // When/then
        (m != l).Should().BeTrue();
        (objectRef != l).Should().BeTrue();
        (objRef != l).Should().BeTrue();
        (!m.Equals(l)).Should().BeTrue();
        (!objectRef.Equals(l)).Should().BeTrue();
    }
}
