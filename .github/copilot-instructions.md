# High-level layout of the `sharpy` repository

The Sharpy standard library (implemented as a .NET library in C#) is located
at `./dotnet` relative to the root of this repository. Eventually it should
be migrated to the main source directory under `./src/` but for not it is
kept separate until it's ready to integrate. The compiler toolchain and some
other stubs are under `./src/Sharpy*`.

# Invocable tools in the workspace

In each of those subdirectories, you can use native `dotnet` commands to build,
test, and format the code.

# General guidance

There's no need to create summary documents of your changes unless explicitly
requested to.

There's also no need to create demo programs unless explicitly requested to.
