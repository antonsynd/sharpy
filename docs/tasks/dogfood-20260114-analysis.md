# Dogfood Run Analysis - January 14, 2026

**Run Summary:** 25 iterations, 23 successful (92%), 1 failed, 1 skipped

## ✅ Fixed: Float Floor Division CS0121 Ambiguity

**Issue:** `float_variables/medium` test failed with:
```
Assembly compilation failed:
  dogfood_test.cs(36,46): error CS0121: The call is ambiguous between the following methods or properties: 'Math.Floor(double)' and 'Math.Floor(decimal)'
```

**Root Cause:** When floor division `//` was used with float operands (e.g., `7.0 // 2.0`), the generated code called `Math.Floor(left / right)` without an explicit cast. Since `float / float` produces `float` in C#, and `float` can implicitly convert to both `double` and `decimal`, the compiler couldn't determine which `Math.Floor` overload to use.

**Fix Applied:** Modified `GenerateFloorDivision()` in [src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs](../../../src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs#L3235) to always cast the division result to `double` before calling `Math.Floor`, resolving the overload ambiguity.

**Test Added:** `FloatFloorDivision_ShouldNotCauseAmbiguity` in [DivisionDeviationTests.cs](../../../src/Sharpy.Compiler.Tests/Integration/SpecDeviations/DivisionDeviationTests.cs)

---

## ✅ Correctly Skipped: nested_if_in_loop/medium

**Status:** Working as intended - no action required

**What happened:** The dogfood tool generated a test case where the expected output in the code comments didn't match what Python actually produces. The validation step detected this mismatch and skipped the test.

**Skip reason from log:**
```
Invalid expected output (Python says: -2\n-1\n3\n5\n8\n3\n13\n4\n18\n2)
```

This demonstrates the dogfood validation is working correctly - it catches LLM hallucinations in expected outputs before running tests against the compiler.

---

## No Remaining Tasks

All issues from this dogfood run have been addressed. The compiler is working correctly.

### For Future Reference

If similar issues occur, here are investigation steps:

1. **Compilation failures** - Check `dogfood_output/issues/*/error.txt` for the C# compiler error
2. **Read the generated source** - Look at `source.spy` to understand what Sharpy code was generated
3. **Trace to codegen** - Search for relevant operators/features in `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
4. **Verify with integration test** - Add a regression test in `src/Sharpy.Compiler.Tests/Integration/`

### Useful Commands

```bash
# Run all tests
dotnet test

# Run specific tests
dotnet test --filter "FullyQualifiedName~DivisionDeviation"

# Compile a specific Sharpy file
dotnet run --project src/Sharpy.Cli -- build snippets/test.spy
```
