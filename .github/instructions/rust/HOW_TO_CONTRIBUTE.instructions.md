---
applyTo: "**"
---

You can build the Sharpy compiler toolchain (implemented as a Rust project) using:

```bash
cargo build
```

You can run the tests using:

```bash
chiri pkg -- test --target rs
```

You can run only selects tests using the `--test` option:

```bash
cargo test --test <test_name> --test <another_test_name>
```

```bash
cargo fmt
```

If you need to invoke any `cargo` tool directly, just use `cargo` to invoke the Cargo.
This can be used to run `clippy` for linting, among other things if you see fit.
