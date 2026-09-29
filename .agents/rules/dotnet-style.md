---
trigger: glob
globs: "**/*.cs"
description: "C# and .NET coding conventions for SportsCenterManagement."
---

# C# and .NET Rules

- Target .NET 10 and preserve nullable reference type annotations.
- Never use `var` in C# source code or tests. Declare the concrete type for every
  local variable, including `using`, `await using`, arrays, query results, and
  `out` variables.
- This prohibition applies to new code, modified code, restored code, and code
  copied from an existing method. Do not preserve or introduce `var` merely to
  match an older file.
- Before completing a task, search every added or modified C# line for `var` and
  replace it with an explicit type.
- Preserve the namespace style already used by the file being edited.
- Use constructor injection through an interface for application services.
- Keep controllers thin. Business decisions, database queries, password work,
  and token creation belong in services.
- Keep API request and response contracts in `APIViewModel`; do not expose EF
  Core entities directly from controllers.
- Use the `Async` suffix for asynchronous methods and await asynchronous EF Core
  and ASP.NET Core APIs.
- Do not use null-forgiving operators to hide an unvalidated nullable value.
- Do not duplicate authentication, hashing, token, or duplicate-account logic
  across role-specific methods when an existing service can own that behavior.
- Catch exceptions only when the method can handle them meaningfully. Do not
  silently convert every exception into `false` or `null` when that would hide a
  server or database failure.
- Keep comments rare and useful. Prefer clear names and small methods.
- Match test coverage to behavior changed, especially authentication, account
  lockout, duplicate detection, and authorization.
