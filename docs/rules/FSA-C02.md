# FSA-C02

## Metadata
- **Severity:** Critical
- **Message:** Option.get / .Value Without Guard
- **Related Rules:** FSA-C09

## Explanation
Option.get bypasses type safety and can cause runtime NullReferenceExceptions. Use pattern matching or Option.bind.
