# Odyssey VTT Error Code Registry

**Status:** Active  
**Owner:** Odyssey.Application  
**Introduced by:** ODY-S00-004  
**Last updated:** 2026-08-11

This registry contains only ErrorCodes introduced in production source. Do not pre-register future gameplay, persistence, networking, protocol, or UI codes.

ErrorCode format is lowercase dot-separated `<area>.<subject>.<condition>`. Segments use lowercase ASCII letters, digits, and underscores. Spaces, provider numeric codes, version suffixes, and reused deprecated or reserved codes are forbidden.

| Code | Owner module | Category | Default SafeReasonCode | Default UserMessageKey | Default RetryDirective | Introduced version | Status | Allowed metadata keys | Security notes | Test reference |
|---|---|---|---|---|---|---|---|---|---|---|
| `application.validation.invalid` | `Odyssey.Application` | `Validation` | `InvalidRequest` | `errors.application.validation_invalid` | `DoNotRetry` | `0.1.0` | Active | `limit.max` | Public-safe validation foundation code; details must not contain raw rejected values, secrets, stack traces, SQL, absolute paths, or hidden entity payloads. | `TC-RESULT-002`, `TC-RESULT-004` |
| `application.internal.unexpected` | `Odyssey.Application` | `Internal` | `UnexpectedError` | `errors.application.unexpected` | `ManualRecoveryRequired` | `0.1.0` | Active | - | Public-safe unexpected-failure foundation code; diagnostic data must stay behind opaque `DiagnosticId` and must not be embedded in `Error`. | `TC-RESULT-002`, `TC-RESULT-003` |
| `application.bootstrap.configuration_invalid` | `Odyssey.Application` | `Validation` | `InvalidRequest` | `errors.runtime.configuration_invalid` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe ODY-S00-006 bootstrap configuration failure; response must not expose Unity scene paths, local directories, stack traces, or internal composition details. | `TC-CMP-002`, `TC-CMP-016` |
| `application.bootstrap.initialization_cancelled` | `Odyssey.Application` | `Cancelled` | `OperationCancelled` | `errors.runtime.startup_cancelled` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe ODY-S00-006 startup cancellation code; cancellation must not be reported as validation or unexpected failure. | `TC-CMP-011` |
| `application.bootstrap.composition_invalid` | `Odyssey.Application` | `Precondition` | `ActionNotAllowed` | `errors.runtime.composition_invalid` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe ODY-S00-006 composition failure; response must not expose raw exception messages, service graphs, scene internals, or local paths. | `TC-CMP-021` |
| `application.bootstrap.unexpected` | `Odyssey.Application` | `Internal` | `UnexpectedError` | `errors.runtime.unexpected_startup_failure` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe ODY-S00-006 unexpected startup failure; details must be correlated only through opaque `DiagnosticId`. | `TC-DIAG-016`, `TC-DIAG-017` |
| `application.developer.probe_rejected` | `Odyssey.Application` | `RuleViolation` | `ActionNotAllowed` | `errors.developer_shell.probe_rejected` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe DeveloperShell-only technical probe rejection; it must not be used for product gameplay or bootstrap composition failures. | `TC-DIAG-024`, `TC-UNITY-SHELL-001` |
| `application.command.identity_mismatch` | `Odyssey.Application` | `Security` | `ActionNotAllowed` | `errors.application.command_identity_mismatch` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe duplicate command identity mismatch; response must not reveal the original stored command result, fingerprint, payload, actor, hidden state, or diagnostic details. | `TC-CMD-004` |
| `application.random.invalid_range` | `Odyssey.Application` | `Validation` | `InvalidRequest` | `errors.application.validation_invalid` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe RNG range validation; failure must consume zero raw RNG steps and must not expose RNG key, state, or secret material. | `TC-RNG-005` |
| `application.random.draw_index_mismatch` | `Odyssey.Application` | `Validation` | `InvalidRequest` | `errors.application.validation_invalid` | `DoNotRetry` | `0.1.0` | Active | - | Public-safe RNG draw-index validation; failure must consume zero raw RNG steps and must not expose RNG key, state, or secret material. | `TC-RNG-005` |
