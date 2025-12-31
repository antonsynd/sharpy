# Class Methods

Class methods as in the Python `@classmethod` decorator do not exist in Sharpy. Sharpy classes (and structs, etc.) can only have either instance methods, static methods, and dunder methods. With the exception of dunder methods, these behave as they do in C#, which is that instance methods are inherited, and static methods are not. (Dunder methods are a special case that depends on the dunder method in question).
