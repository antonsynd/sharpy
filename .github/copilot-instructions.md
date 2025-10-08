The Sharpy standard library (implemented as a .NET library in C#) is located
at `./dotnet` relative to the root of this repository.

The Sharpy compiler toolchain (implemented as a Rust project) is located at
`./rust` relative to the root of this repository.

In each of those subdirectories, you can use the native `dotnet` or `cargo`
commands to build, test, and format the code. However, there is also a
command-line tool called `chiri` that can be used to build, test, and format
the code in both subdirectories from the root of the repository. `chiri` is
located at `~/.chiri/bin/chiri` but should be on PATH anyway.

Run `chiri pkg -- --help` to see what options are available. It itself
invokes `./build_tools/bin/build_sharpy` relative to the root of the repository.
