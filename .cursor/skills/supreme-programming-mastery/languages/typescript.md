# TypeScript / JavaScript

## Defaults

- Strict TypeScript where possible
- `===` not `==`
- `??` for nullish coalescing
- `async/await` + top-level error handling

## Pitfalls

| Issue | Fix |
|-------|-----|
| `any` creep | strict types |
| Closure in loop | `let` / block scope |
| Unhandled rejection | catch at boundary |
| JSON date parsing | explicit types |

## Stack detect

`package.json` → scripts.test / scripts.build

## Node verify

`npm test` or `pnpm test` or `npm run build`
