# High-level layout of the `sharpy` repository

The Sharpy standard library (implemented as a .NET library in C#) is located
at `./src/Sharpy.Core` relative to the root of this repository. The compiler
toolchain is under `./src/Sharpy.Compiler`. There is also compiler CLI located
under `./src/Sharpy.Cli`.

Tests are located under `./src/Sharpy.Core.Tests` and
`./src/Sharpy.Compiler.Tests`.

# Invocable tools in the workspace

In each of those subdirectories, you can use native `dotnet` commands to build,
test, and format the code.

# General guidance

There's no need to create summary documents of your changes unless explicitly
requested to.

There's also no need to create demo programs unless explicitly requested to.
