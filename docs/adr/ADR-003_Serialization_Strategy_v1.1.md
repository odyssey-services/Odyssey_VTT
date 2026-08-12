# ADR-003 - Serialization Strategy

**Document:** `docs/adr/ADR-003_Serialization_Strategy_v1.1.md`  
**ADR:** ADR-003  
**Version:** 1.1  
**Date:** 12 August 2026  
**Status:** Accepted  
**Supersedes:** `docs/adr/ADR-003_Serialization_Strategy_v1.0.md` only for serializer implementation mechanism, AOT implementation rules, and `.odcamp` manifest ownership clarification.

---

# 1. Decision

Odyssey keeps the ADR-003 v1.0 serialization semantics: explicit versioned boundary DTOs, stable `ContractType` and `ContractVersion`, canonical UTF-8 JSON, deterministic property order, immutable stored event payload bytes, canonical hashes, pure upcasters, parser ceilings, duplicate-property rejection, required/null distinction, strict type validation, enum/ID/time rules, no CLR type names, no direct Domain aggregate serialization, separated profiles, and .NET / Unity Mono / Windows x64 IL2CPP parity evidence.

The serializer implementation mechanism changes:

```text
Odyssey uses explicit hand-written JSON codecs for release-critical contracts.
The approved JSON parsing/writing primitive is Newtonsoft.Json 13.0.2 low-level streaming API.
```

Approved dependency baselines:

```text
Unity package: com.unity.nuget.newtonsoft-json@3.2.2
Underlying Newtonsoft product version: 13.0.2
AssemblyVersion: 13.0.0.0
Pure .NET package: Newtonsoft.Json 13.0.2
```

The normative release-critical contract path is:

```text
(ContractType, ContractVersion) -> explicit codec -> validator -> mapper/upcaster
```

The registry is explicit and compile-time constructed. Runtime discovery is prohibited.

# 2. Allowed and Prohibited APIs

Allowed for approved release-critical JSON codecs:

- `JsonTextReader`, `JsonTextWriter`, and equivalent low-level Newtonsoft streaming primitives.
- Explicit hand-written encode/decode methods.
- Explicit codec registration by stable contract type and version.
- Explicit validators and pure mappers/upcasters.

Prohibited on release-critical contract paths:

- `JsonConvert.SerializeObject`.
- `JsonConvert.DeserializeObject`.
- Automatic `JsonSerializer` object mapping.
- `TypeNameHandling`.
- `DefaultContractResolver` or reflection contract discovery.
- `Type.GetType`, assembly scanning, CLR type-name discriminators, or assembly-qualified names.
- Arbitrary `object` graph serialization/deserialization.
- Runtime-generated serializers.
- Serializer annotations on Domain aggregates.

ADR-003 v1.1 removes the ADR-003 v1.0 requirement for `System.Text.Json`, `JsonSerializerContext`, `JsonTypeInfo`, `JsonSerializableAttribute`, and System.Text.Json source generators on production release-critical contracts.

# 3. Preserved Contract Semantics

All of these ADR-003 v1.0 rules remain authoritative:

- Every external or durable boundary uses an explicit versioned DTO.
- Domain aggregates are not serialized directly as file, database, transport, diagnostic, or fixture contracts.
- Contract identity is a stable `ContractType` string plus positive integer `ContractVersion`; CLR names are never contract identifiers.
- Canonical JSON is UTF-8 without BOM and uses deterministic explicit property order.
- Command fingerprints and integrity hashes are built from canonical bytes, not runtime object identity, `GetHashCode()`, incidental serializer output, transport metadata, or process-local values.
- Stored event payload bytes are immutable and are not rewritten during read, map, or upcast.
- Old payloads are read through pure upcaster chains; missing mandatory paths produce controlled compatibility failures before mutation.
- Parser limits cover size, depth, count, duplicate properties, comments/trailing commas where prohibited, UTF-8 validity, numeric edge cases, timestamps, IDs, enum tokens, required fields, and null semantics.
- JSON does not replace SQLite typed columns, constraints, revisions, or relational integrity.
- Binary assets are not embedded as base64 JSON.
- Transport, persistence, interchange, diagnostics, configuration, and fixture profiles remain separate.
- Redaction happens before diagnostic serialization.
- Golden fixtures and parity vectors are reviewed, deterministic, and not auto-updated to make tests pass.

# 4. Module Ownership

`Odyssey.Application` owns:

- release-critical canonical JSON codec abstractions and explicit codec registry;
- command fingerprint canonical material codecs;
- event payload codec contract abstractions;
- diagnostic JSON codec contract integration required by ADR-010 v1.1;
- `.odcamp` manifest contract DTO, `ContractType`/`ContractVersion`, semantic validation, compatibility result, and interchange codec contract.

`Odyssey.Persistence` later owns:

- archive and filesystem I/O;
- the physical `.odcamp` container;
- SQLite backup packaging and restoration;
- filesystem paths, atomic replacement, and storage adapter details.

Application must not perform filesystem/archive I/O. Persistence must not redefine manifest semantics.

`Odyssey.Domain` remains serializer-free. It must not depend on Newtonsoft.Json, System.Text.Json, serializer attributes, SQLite annotations, or diagnostic logging.

# 5. Evidence Behind v1.1

System.Text.Json 10.0.11 feasibility results:

- pure .NET source generation: PASS;
- Unity 6000.4 Editor/Mono coherent dependency probe: PASS;
- Unity 6000.4 Player managed compile: FAIL because System.Text.Json runtime references were absent in Player managed compilation;
- Unity 6000.5.7f1 baseline: PASS;
- Unity 6000.5.7f1 actual script compiler: `Microsoft.CodeAnalysis 3.7.0.0`;
- oldest System.Text.Json 10.0.11 source generator variant requires newer Roslyn;
- Unity 6.5 System.Text.Json 10 source-generation path: blocked.

Explicit Newtonsoft streaming feasibility results:

- Unity package `com.unity.nuget.newtonsoft-json@3.2.2`;
- Newtonsoft.Json product version `13.0.2`;
- .NET version parity: PASS;
- pure .NET compile/round-trip: PASS;
- Unity Mono/EditMode: PASS;
- Windows x64 IL2CPP build: PASS;
- Player launch: PASS;
- canonical vector parity: PASS;
- duplicate property rejection: PASS;
- missing required property rejection: PASS;
- wrong-token rejection: PASS;
- reflection object serialization: NOT USED;
- linker/preservation workaround: NOT REQUIRED.

Compatibility evidence vector only, not a future product contract:

```json
{"contractType":"odyssey.serialization-aot-smoke","contractVersion":1,"sequence":42,"message":"Ready","note":null}
```

SHA-256:

```text
75efac616f7b29a8aa2c9690dcdf85fae122848125092b81ac4443958baa7e68
```

# 6. Normative Effect

From activation in `ACTIVE_DOCUMENTATION_BASELINE`, any new release-critical JSON contract implementation that contradicts this ADR is an architecture defect.

ADR-003 v1.0 remains historical context. Active work must use ADR-003 v1.1.
