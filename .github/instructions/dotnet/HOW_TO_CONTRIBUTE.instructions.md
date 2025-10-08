---
applyTo: "**"
---

You can build the Sharpy standard library (implemented as a .NET library in C#) using:

```bash
chiri pkg -- build --project lib
```

Note that this does not build the tests.

You can optionally add the `--release` flag to build the project library in release mode.

You can build the tests using:

```bash
chiri pkg -- build --project tests
```

Again, you can optionally add the `--release` flag to build the tests in release mode.

You can run the tests using:

```bash
chiri pkg -- test --target cs
```

You can format the C# code using:

```bash
chiri pkg -- fmt --target cs
```

If you need to invoke any `dotnet` tool directly, just use `dotnet` to invoke the .NET CLI. For example, to run the tests directly with `dotnet`, you can use:

```bash
dotnet test
```
