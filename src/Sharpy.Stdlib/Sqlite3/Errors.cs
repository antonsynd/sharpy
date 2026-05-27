using System;

namespace Sharpy
{
    /// <summary>Base exception for all sqlite3-related errors.</summary>
    [SharpyModuleType("sqlite3", "Error")]
    public class Sqlite3Error : Exception
    {
        public Sqlite3Error() : base() { }
        public Sqlite3Error(string message) : base(message) { }
        public Sqlite3Error(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Exception raised for errors related to the database.</summary>
    [SharpyModuleType("sqlite3", "DatabaseError")]
    public class Sqlite3DatabaseError : Sqlite3Error
    {
        public Sqlite3DatabaseError() : base() { }
        public Sqlite3DatabaseError(string message) : base(message) { }
        public Sqlite3DatabaseError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Exception raised for errors related to the database's operation, such as SQL syntax errors.</summary>
    [SharpyModuleType("sqlite3", "OperationalError")]
    public class Sqlite3OperationalError : Sqlite3DatabaseError
    {
        public Sqlite3OperationalError() : base() { }
        public Sqlite3OperationalError(string message) : base(message) { }
        public Sqlite3OperationalError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Exception raised when the relational integrity of the database is affected, such as a foreign key or uniqueness constraint violation.</summary>
    [SharpyModuleType("sqlite3", "IntegrityError")]
    public class Sqlite3IntegrityError : Sqlite3DatabaseError
    {
        public Sqlite3IntegrityError() : base() { }
        public Sqlite3IntegrityError(string message) : base(message) { }
        public Sqlite3IntegrityError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Exception raised for programming errors, such as operating on a closed database or cursor.</summary>
    [SharpyModuleType("sqlite3", "ProgrammingError")]
    public class Sqlite3ProgrammingError : Sqlite3DatabaseError
    {
        public Sqlite3ProgrammingError() : base() { }
        public Sqlite3ProgrammingError(string message) : base(message) { }
        public Sqlite3ProgrammingError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Exception raised for errors related to the database interface rather than the database itself.</summary>
    [SharpyModuleType("sqlite3", "InterfaceError")]
    public class Sqlite3InterfaceError : Sqlite3Error
    {
        public Sqlite3InterfaceError() : base() { }
        public Sqlite3InterfaceError(string message) : base(message) { }
        public Sqlite3InterfaceError(string message, Exception innerException) : base(message, innerException) { }
    }
}
