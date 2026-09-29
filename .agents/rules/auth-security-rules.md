---
trigger: glob
globs: "Services/AuthService/**/*.cs, Services/AccessTokenService/**/*.cs, Services/PasswordHashService/**/*.cs, Services/Utils/JwtOptions.cs, SportsCenterManagement/Controllers/AuthController.cs, SportsCenterManagement/Filter/**/*.cs, SportsCenterManagement/Program.cs"
description: "Authentication, JWT, password hashing, role authorization, logout blacklist, and account lockout rules."
---

# Authentication and Security Rules

- Keep password hashing and verification in `IPasswordHashService` and
  `PasswordHashService`. Use BCrypt; do not replace it with plain SHA hashing or
  a hard-coded private salt.
- Keep JWT creation in `IAccessTokenService` and `AccessTokenService`. Controllers
  must not construct signing credentials or claims directly.
- Read issuer, audience, signing key, and token lifetime from validated
  configuration. Never hard-code the signing key.
- Tokens must contain the account identifier, email, and role claims expected by
  authorization and `check-token`.
- Validate issuer, audience, lifetime, signing key, and role claim mapping. Keep
  `ClockSkew` intentional.
- The only current role names are `CenterManager`, `Coach`, `Member`, and
  `Receptionist`. Do not invent `Admin`, `Staff`, or `Customer` roles.
- Login failures must not reveal whether an email exists. Apply account status,
  lockout, and failed-attempt rules consistently to every role.
- On successful login, reset failed-login state when required by the use case.
- Logout stores only the presented access token, or preferably its identifier,
  in the `IMemoryCache` blacklist. Set blacklist expiration to the token's
  remaining lifetime instead of a fixed duration.
- An in-memory blacklist is process-local and is cleared on restart. Do not
  describe it as distributed or persistent. If the API is scaled to multiple
  instances, propose a distributed cache as a separate architecture change.
- Protected endpoints must use ASP.NET Core authentication and authorization.
  Apply the blacklist filter consistently to endpoints that must reject logged
  out tokens.
- Never log passwords, password hashes, complete JWTs, signing keys, or sensitive
  claims.
- Add or update tests for successful login, invalid credentials, role mismatch,
  lockout, token validation, logout, and blacklisted-token rejection when those
  behaviors change.
