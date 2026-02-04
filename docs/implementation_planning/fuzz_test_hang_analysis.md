## Fuzz Test Infinite Loop Investigation Summary

### Problem
The Sharpy compiler test suite hangs indefinitely when FuzzTests are included. All 4,836 non-fuzz tests pass in ~24 seconds, but including FuzzTests causes the test runner to hang at 100% CPU.

### Discovery Method
1. Started tests, waited for hang
2. Captured process dump: `dotnet-dump collect -p <testhost_pid> -o /tmp/testhost-hang.dmp`
3. Analyzed with: `dotnet-dump analyze /tmp/testhost-hang.dmp` → `parallelstacks` / `clrstack -all`

### Key Stack Traces from Dump

**Thread stuck in FuzzTests:**
```
Sharpy.Compiler.Tests.Fuzz.FuzzTests.Compiler_SyntaxErrors_NeverThrowsUnhandledException(Int32)
  @ FuzzTests.cs:165
    → Sharpy.Compiler.Compiler.Compile()
    → Sharpy.Compiler.Parser.Parser.ParseModule()
    → Parser.ParseStatement()
    → Parser.ParseSimpleStatement()
    → Parser.ParseExpression()
    → Parser.ParseWalrusExpression()
    → ... (full expression parsing chain)
    → Parser.ParsePostfix()
    → Parser.ParsePrimary() @ Parser.Primaries.cs:525
```

### Likely Root Cause
The parser enters an infinite loop when processing certain randomly-generated inputs from `SharplyFuzzer.GenerateWithSyntaxErrors()`. The issue is likely in one of:

1. **`Parser.ParsePostfix()` (Parser.Expressions.cs:624)** - Has a `while (true)` loop that may not advance tokens properly on malformed input
2. **`Parser.ParsePrimary()` (Parser.Primaries.cs:525)** - The `default` case throws `ParserAbortException`, but if error recovery doesn't advance the token, calling code may retry infinitely
3. **Lambda parameter parsing (Parser.Primaries.cs:482-500)** - Has `do { ... } while (true)` that could loop if `ExpectIdentifier()` fails without advancing

### Relevant Files
- `/src/Sharpy.Compiler.Tests/Fuzz/FuzzTests.cs` - Test harness (seeds: 42, 123, 7777, 2025, 9999)
- `/src/Sharpy.Compiler.Tests/Fuzz/SharplyFuzzer.cs` - Input generator (`GenerateWithSyntaxErrors()`, `GenerateRandomTokens()`)
- `/src/Sharpy.Compiler/Parser/Parser.Expressions.cs` - `ParsePostfix()` at line 624
- `/src/Sharpy.Compiler/Parser/Parser.Primaries.cs` - `ParsePrimary()` at line 15

### How to Reproduce
```bash
# This will hang:
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~FuzzTests"

# This passes (excludes fuzz tests):
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName!~Fuzz"
```

### Suggested Investigation Steps

1. **Isolate the problematic seed/iteration:**
   ```csharp
   // In FuzzTests.cs, add logging to find exact input:
   var input = fuzzer.GenerateWithSyntaxErrors();
   Console.WriteLine($"Seed {seed}, iteration {i}: {input.Replace("\n", "\\n")}");
   ```

2. **Check parser token advancement:**
   - In `ParsePostfix()` while loop, verify all branches call `Advance()` or break
   - In `ParsePrimary()` default case, ensure `ReportError` advances or the caller doesn't retry

3. **Add timeout to fuzz iterations:**
   ```csharp
   using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
   try {
       var result = compiler.Compile(input, "fuzz.spy", cts.Token);
   } catch (OperationCanceledException) {
       // Log the input that caused timeout
   }
   ```

4. **Look for loops that don't advance on error:**
   ```bash
   grep -n "while.*true\|do.*while" src/Sharpy.Compiler/Parser/*.cs
   ```
   Check each loop has a guaranteed exit when tokens are exhausted or malformed.

### Quick Fix (Skip Fuzz Tests)
Add `[Trait("Category", "Fuzz")]` to fuzz tests and exclude from CI:
```bash
dotnet test --filter "Category!=Fuzz"
```

### Notes
- FuzzTests are from Phase 6 (Advanced Type Safety & Quality), not blocking for Phase 2
- The parser likely needs a "max recursion depth" or "tokens consumed" guard
- Consider adding `CancellationToken` support to the parser (Phase 5 scope)
