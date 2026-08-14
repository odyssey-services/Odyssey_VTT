# Third-Party Notices

This repository is private and All Rights Reserved. The following approved third-party packages are used by the repository.

| Package | Version | Purpose | License | Scope | Approval |
|---|---:|---|---|---|---|
| Newtonsoft.Json | 13.0.2 | Explicit deterministic release-critical JSON streaming codecs | MIT | Pure .NET `Odyssey.Application` bridge | ADR-003 v1.1 / Technical Development Baseline v0.4 / ODY-S00-007 |
| com.unity.nuget.newtonsoft-json | 3.2.2 | Unity package distribution of Newtonsoft.Json 13.0.2 (`AssemblyVersion` 13.0.0.0) for explicit streaming codecs | MIT / Unity package distribution | Unity `Odyssey.Application` package | ADR-003 v1.1 / Technical Development Baseline v0.4 / ODY-S00-007 |

Approved tooling referenced by repository policy:

| Tool | Purpose | Notes |
|---|---|---|
| Git | Version control | Existing approved tooling |
| Git LFS | Large binary pointer management | Required by Technical Development Baseline |
| GitHub repository features | Hosting, pull requests, branch protection | Owner-controlled repository configuration |
| actions/checkout | GitHub Actions source checkout pinned to `d23441a48e516b6c34aea4fa41551a30e30af803` | MIT; approved by Technical Development Baseline v0.5 / ODY-S00-008 |
| actions/setup-dotnet | GitHub Actions .NET SDK setup pinned to `26b0ec14cb23fa6904739307f278c14f94c95bf1` | MIT; approved by Technical Development Baseline v0.5 / ODY-S00-008 |
| actions/upload-artifact | GitHub Actions bounded evidence upload pinned to `330a01c490aca151604b8cf639adc76d48f6c5d4` | MIT; approved by Technical Development Baseline v0.5 / ODY-S00-008 |

Future dependencies must be approved by the active task or ADR and recorded here with license evidence.
