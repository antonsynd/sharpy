---
applyTo: "**"
---

You can build the Sharpy compiler toolchain (implemented as a Rust project) using the `chiri` command-line tool:

```bash
chiri pkg -- build --project compiler
```

You can also just use `cargo` directly if you prefer:
```bash
cargo build
```

You can run the tests using:

```bash
chiri pkg -- test --target rs
```

Or use `cargo` directly:

```bash
cargo test
```

Note that only by using `cargo` directly can you run tests with additional
filtering, e.g.:

```bash
cargo test --test <test_name> --test <another_test_name>
```

You can format the Rust code using:

```bash
chiri pkg -- fmt --target rs
```

Or use `cargo` directly:

```bash
cargo fmt
```

If you need to invoke any `cargo` tool directly, just use `cargo` to invoke the .NET CLI.
This can be used to run `clippy` for linting, among other things if you see fit.
