---
trigger: glob
globs: "SportsCenterManagement/Controllers/**/*.cs, APIViewModel/**/*.cs"
description: "HTTP API controller, request model, response model, validation, and status-code conventions."
---

# API Endpoint Rules

- Controllers coordinate HTTP concerns only. Put business logic and EF Core
  queries in the appropriate service.
- Use request and response models from `APIViewModel`. Do not accept or return
  database entities as public API contracts.
- Preserve an existing route contract unless the user explicitly requests a
  breaking route change. New routes should follow one consistent naming style.
- Rely on `[ApiController]` model validation where appropriate. If validation is
  handled manually, return useful validation errors without exposing internals.
- Use status codes according to the actual outcome:
  - `200 OK` for successful reads or commands with a response.
  - `201 Created` for a newly created resource when its location is available.
  - `204 No Content` for a successful command with no response body.
  - `400 Bad Request` for malformed input or validation failure.
  - `401 Unauthorized` for missing, invalid, expired, or blacklisted credentials.
  - `403 Forbidden` for an authenticated account lacking permission.
  - `404 Not Found` when the requested resource does not exist.
  - `409 Conflict` for duplicate email, phone, member code, or conflicting state.
  - `500 Internal Server Error` only for unexpected server failures.
- Do not return `500` for expected duplicate or validation outcomes.
- Use `[AllowAnonymous]` only for intentionally public endpoints. Use
  `[Authorize]` or role-based authorization for protected operations.
- Do not expose stack traces, connection details, hashes, tokens, or internal
  exception messages in API responses.
- Update NSwag-visible response metadata when an endpoint has multiple important
  response outcomes.
