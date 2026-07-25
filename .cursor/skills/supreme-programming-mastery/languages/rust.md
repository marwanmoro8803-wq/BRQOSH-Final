# Rust

## Ownership model

- Borrow checker is the guide
- `?` over `unwrap()` in production
- `Send`/`Sync` for concurrency

## Pitfalls

use-after-move, data races (compile-time), panic in library code

## Verify

`cargo build` `cargo test` `cargo clippy`
