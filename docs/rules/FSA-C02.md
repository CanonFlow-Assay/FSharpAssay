# FSA-C02

## Metadata
- **Severity:** Critical
- **Message:** Option.get / .Value Without Guard
- **Related Rules:** FSA-C09

## Explanation
Option.get is partial: evaluating it on None raises an argument exception. Use pattern matching or Option.bind when None is a normal outcome. This detector does not prove arbitrary guards or caller invariants.

## Authority and repair

This rule remains experimental and nonblocking. Preserve the intended None behavior and test affected callers before applying a repair. See the [rule audit](../refresh/rule-audit.md).
