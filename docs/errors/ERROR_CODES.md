# Odyssey VTT Error Code Registry

**Status:** Active  
**Owner:** Odyssey.Application  
**Introduced by:** ODY-S00-004  
**Last updated:** 2026-08-10

This registry contains only ErrorCodes introduced in production source. Do not pre-register future gameplay, persistence, networking, protocol, or UI codes.

ErrorCode format is lowercase dot-separated `<area>.<subject>.<condition>`. Segments use lowercase ASCII letters, digits, and underscores. Spaces, provider numeric codes, version suffixes, and reused deprecated or reserved codes are forbidden.

| Code | Owner module | Category | Default SafeReasonCode | Default RetryDirective | Introduced version | Deprecated / Reserved status | Security notes | Test reference |
|---|---|---|---|---|---|---|---|---|
| `application.validation.invalid` | `Odyssey.Application` | `Validation` | `InvalidInput` | `DoNotRetry` | ODY-S00-004 | Active | Public-safe validation foundation code; details must not contain raw rejected values, secrets, stack traces, SQL, absolute paths, or hidden entity payloads. | `TC-RESULT-002`, `TC-RESULT-004` |
| `application.internal.unexpected` | `Odyssey.Application` | `Internal` | `UnexpectedError` | `ManualRecoveryRequired` | ODY-S00-004 | Active | Public-safe unexpected-failure foundation code; diagnostic data must stay behind opaque `DiagnosticId` and must not be embedded in `Error`. | `TC-RESULT-002`, `TC-RESULT-003` |
