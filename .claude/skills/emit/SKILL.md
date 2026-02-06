---
name: emit
description: Compile a .spy file and inspect generated C#, AST, or tokens
disable-model-invocation: true
argument-hint: <file.spy>
---

Inspect intermediate representations for the given Sharpy source file.

Run all four emit commands and present the results:

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp $ARGUMENTS
dotnet run --project src/Sharpy.Cli -- emit ast $ARGUMENTS
dotnet run --project src/Sharpy.Cli -- emit tokens $ARGUMENTS
dotnet run --project src/Sharpy.Cli -- emit parse $ARGUMENTS
```

If any command fails, show the error and continue with the rest.
