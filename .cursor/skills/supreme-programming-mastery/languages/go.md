# Go

## Idioms

- errors are values — handle explicitly
- context for cancellation
- interfaces small

## Pitfalls

| Issue | Fix |
|-------|-----|
| nil map write | `make` first |
| goroutine leak | context cancel |
| loop var capture | copy in goroutine |

## Verify

`go test ./...` `go vet`
