---
name: dogfood-run
description: Run dogfooding iterations to test the Sharpy compiler
argument-hint: "[number_of_iterations]"
---

Run the Sharpy dogfooding pipeline to generate, compile, and verify AI-generated test cases.

## Steps

1. **Parse iteration count from `$ARGUMENTS`**. If empty or non-numeric, default to 5.

2. **Run the dogfooding tool in the background** using Bash with `run_in_background: true`:

   ```bash
   cd build_tools && python3 -m sharpy_dogfood run -n <N> --verbose 2>&1
   ```

   Replace `<N>` with the parsed iteration count.

3. **Inform the user** that dogfooding is running in the background. Each iteration involves AI code generation + compilation + execution, so estimate roughly 1-2 minutes per iteration.

4. **Wait for completion** by reading the background task output.

5. **Present results**. After the task completes:
   - Read `dogfood_output/SUMMARY.md` (relative to the project root `/Users/anton/Documents/github/sharpy/`) and present its contents to the user.
   - If the summary mentions failures or issues, suggest running `/dogfood-analyze` to classify them by root cause.
