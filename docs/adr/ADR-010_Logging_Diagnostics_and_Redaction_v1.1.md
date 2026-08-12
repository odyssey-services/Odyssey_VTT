# ADR-010 - Logging, Diagnostics and Redaction

**Document:** `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.1.md`  
**ADR:** ADR-010  
**Version:** 1.1  
**Date:** 12 August 2026  
**Status:** Accepted  
**Supersedes:** `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md` only where diagnostic JSON serialization explicitly required source-generated System.Text.Json.

---

# 1. Decision

ADR-010 v1.0 logging, diagnostics, and redaction semantics remain unchanged.

The diagnostic JSON serialization mechanism is aligned with ADR-003 v1.1:

```text
LogEventV1 serializes through the explicit DiagnosticJson codec defined under ADR-003 v1.1 and must preserve .NET / Unity Mono / Windows x64 IL2CPP canonical/compatibility evidence.
```

This replaces the ADR-010 v1.0 rule that `LogEventV1` serializes through a source-generated System.Text.Json context.

# 2. Preserved Semantics

All ADR-010 v1.0 rules remain authoritative, including:

- `LogEventV1` shape and meaning;
- typed `EventCode` registry;
- allowlisted safe properties;
- data classifications;
- redaction before every sink;
- memory sink;
- rolling JSONL sink;
- queue and backpressure behavior;
- rotation, retention, and emergency sink behavior;
- crash marker behavior;
- diagnostic bundle constraints;
- no remote telemetry or automatic crash upload in MVP;
- no arbitrary object logging;
- no `ToString()` diagnostics for commands, events, DTOs, exceptions, or user content;
- no secrets, owner keys, RNG keys, private chat, hidden gameplay data, personal data, stack traces, SQL, or full local paths in user-visible or normal diagnostic outputs.

# 3. Normative Effect

Diagnostic JSON code must use the explicit hand-written codec approach approved by ADR-003 v1.1. It must not rely on automatic Newtonsoft object mapping, `JsonConvert`, `TypeNameHandling`, reflection contract discovery, runtime-generated serializers, or System.Text.Json source-generated contexts for the active production path.

ADR-010 v1.0 remains historical context. Active work must use ADR-010 v1.1.
