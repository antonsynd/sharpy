# Calculator - A simple Sharpy project example

This is a multi-file Sharpy project demonstrating:
- Project file structure
- Module organization with `__init__.spy`
- Cross-file imports
- Package structure

## Project Structure

```
calculator_app/
├── calculator.spyproj       # Project file
├── README.md               # This file
└── src/
    ├── main.spy           # Main entry point
    ├── ui.spy             # User interface module
    └── math/              # Math operations package
        ├── __init__.spy   # Package exports
        ├── basic.spy      # Basic operations
        └── advanced.spy   # Advanced operations
```

## Building

From this directory:
```bash
sharpyc project calculator.spyproj
```

Or from the repo root:
```bash
dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj
```

## Running

```bash
./bin/Debug/net9.0/Calculator.exe
```

## Features Demonstrated

1. **Project Files** - Using `.spyproj` to define multi-file projects
2. **Glob Patterns** - `src/**/*.spy` includes all Sharpy files recursively
3. **Packages** - `math/` directory with `__init__.spy` as a package
4. **Imports** - Cross-file imports between modules
5. **Namespaces** - Generated C# namespaces match directory structure
