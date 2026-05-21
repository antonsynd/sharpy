using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Sqlite3ErrorTests
{
    #region Inheritance Hierarchy

    [Fact]
    public void Sqlite3Error_IsException()
    {
        var error = new Sqlite3Error("test");
        error.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Sqlite3DatabaseError_IsSqlite3Error()
    {
        var error = new Sqlite3DatabaseError("test");
        error.Should().BeAssignableTo<Sqlite3Error>();
    }

    [Fact]
    public void Sqlite3DatabaseError_IsException()
    {
        var error = new Sqlite3DatabaseError("test");
        error.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Sqlite3OperationalError_IsSqlite3DatabaseError()
    {
        var error = new Sqlite3OperationalError("test");
        error.Should().BeAssignableTo<Sqlite3DatabaseError>();
    }

    [Fact]
    public void Sqlite3OperationalError_IsSqlite3Error()
    {
        var error = new Sqlite3OperationalError("test");
        error.Should().BeAssignableTo<Sqlite3Error>();
    }

    [Fact]
    public void Sqlite3OperationalError_IsException()
    {
        var error = new Sqlite3OperationalError("test");
        error.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Sqlite3IntegrityError_IsSqlite3DatabaseError()
    {
        var error = new Sqlite3IntegrityError("test");
        error.Should().BeAssignableTo<Sqlite3DatabaseError>();
    }

    [Fact]
    public void Sqlite3IntegrityError_IsSqlite3Error()
    {
        var error = new Sqlite3IntegrityError("test");
        error.Should().BeAssignableTo<Sqlite3Error>();
    }

    [Fact]
    public void Sqlite3ProgrammingError_IsSqlite3DatabaseError()
    {
        var error = new Sqlite3ProgrammingError("test");
        error.Should().BeAssignableTo<Sqlite3DatabaseError>();
    }

    [Fact]
    public void Sqlite3ProgrammingError_IsSqlite3Error()
    {
        var error = new Sqlite3ProgrammingError("test");
        error.Should().BeAssignableTo<Sqlite3Error>();
    }

    [Fact]
    public void Sqlite3InterfaceError_IsSqlite3Error()
    {
        var error = new Sqlite3InterfaceError("test");
        error.Should().BeAssignableTo<Sqlite3Error>();
    }

    [Fact]
    public void Sqlite3InterfaceError_IsNotSqlite3DatabaseError()
    {
        var error = new Sqlite3InterfaceError("test");
        error.Should().NotBeAssignableTo<Sqlite3DatabaseError>();
    }

    #endregion

    #region Message Propagation

    [Fact]
    public void Sqlite3Error_MessagePropagation()
    {
        var error = new Sqlite3Error("sqlite3 error message");
        error.Message.Should().Be("sqlite3 error message");
    }

    [Fact]
    public void Sqlite3DatabaseError_MessagePropagation()
    {
        var error = new Sqlite3DatabaseError("database error message");
        error.Message.Should().Be("database error message");
    }

    [Fact]
    public void Sqlite3OperationalError_MessagePropagation()
    {
        var error = new Sqlite3OperationalError("operational error message");
        error.Message.Should().Be("operational error message");
    }

    [Fact]
    public void Sqlite3IntegrityError_MessagePropagation()
    {
        var error = new Sqlite3IntegrityError("integrity error message");
        error.Message.Should().Be("integrity error message");
    }

    [Fact]
    public void Sqlite3ProgrammingError_MessagePropagation()
    {
        var error = new Sqlite3ProgrammingError("programming error message");
        error.Message.Should().Be("programming error message");
    }

    [Fact]
    public void Sqlite3InterfaceError_MessagePropagation()
    {
        var error = new Sqlite3InterfaceError("interface error message");
        error.Message.Should().Be("interface error message");
    }

    #endregion

    #region Inner Exception Propagation

    [Fact]
    public void Sqlite3Error_InnerExceptionPropagation()
    {
        var inner = new InvalidOperationException("inner");
        var error = new Sqlite3Error("outer", inner);
        error.InnerException.Should().BeSameAs(inner);
        error.Message.Should().Be("outer");
    }

    [Fact]
    public void Sqlite3DatabaseError_InnerExceptionPropagation()
    {
        var inner = new InvalidOperationException("inner");
        var error = new Sqlite3DatabaseError("outer", inner);
        error.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Sqlite3OperationalError_InnerExceptionPropagation()
    {
        var inner = new InvalidOperationException("inner");
        var error = new Sqlite3OperationalError("outer", inner);
        error.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Sqlite3IntegrityError_InnerExceptionPropagation()
    {
        var inner = new InvalidOperationException("inner");
        var error = new Sqlite3IntegrityError("outer", inner);
        error.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Sqlite3ProgrammingError_InnerExceptionPropagation()
    {
        var inner = new InvalidOperationException("inner");
        var error = new Sqlite3ProgrammingError("outer", inner);
        error.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Sqlite3InterfaceError_InnerExceptionPropagation()
    {
        var inner = new InvalidOperationException("inner");
        var error = new Sqlite3InterfaceError("outer", inner);
        error.InnerException.Should().BeSameAs(inner);
    }

    #endregion

    #region Default Constructor

    [Fact]
    public void Sqlite3Error_DefaultConstructor()
    {
        var error = new Sqlite3Error();
        error.Message.Should().NotBeNullOrEmpty();
        error.InnerException.Should().BeNull();
    }

    [Fact]
    public void Sqlite3DatabaseError_DefaultConstructor()
    {
        var error = new Sqlite3DatabaseError();
        error.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Sqlite3OperationalError_DefaultConstructor()
    {
        var error = new Sqlite3OperationalError();
        error.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Sqlite3IntegrityError_DefaultConstructor()
    {
        var error = new Sqlite3IntegrityError();
        error.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Sqlite3ProgrammingError_DefaultConstructor()
    {
        var error = new Sqlite3ProgrammingError();
        error.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Sqlite3InterfaceError_DefaultConstructor()
    {
        var error = new Sqlite3InterfaceError();
        error.Message.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Catch Hierarchy

    [Fact]
    public void OperationalError_CaughtBySqlite3Error()
    {
        bool caught = false;
        try
        {
            throw new Sqlite3OperationalError("test");
        }
        catch (Sqlite3Error)
        {
            caught = true;
        }

        caught.Should().BeTrue();
    }

    [Fact]
    public void IntegrityError_CaughtBySqlite3DatabaseError()
    {
        bool caught = false;
        try
        {
            throw new Sqlite3IntegrityError("test");
        }
        catch (Sqlite3DatabaseError)
        {
            caught = true;
        }

        caught.Should().BeTrue();
    }

    [Fact]
    public void InterfaceError_NotCaughtBySqlite3DatabaseError()
    {
        bool caughtByDatabaseError = false;
        bool caughtBySqlite3Error = false;
        try
        {
            throw new Sqlite3InterfaceError("test");
        }
        catch (Sqlite3DatabaseError)
        {
            caughtByDatabaseError = true;
        }
        catch (Sqlite3Error)
        {
            caughtBySqlite3Error = true;
        }

        caughtByDatabaseError.Should().BeFalse();
        caughtBySqlite3Error.Should().BeTrue();
    }

    #endregion
}
