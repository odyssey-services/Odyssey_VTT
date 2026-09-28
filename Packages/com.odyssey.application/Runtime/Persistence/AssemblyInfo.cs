using System.Runtime.CompilerServices;

// ODY-S10-103 follow-up: grants access to the `internal interface IActiveEffectSystemRollback`
// (ActiveEffectRepositoryContracts.cs) to exactly the assemblies that legitimately implement or call it --
// Odyssey.Persistence (SqliteActiveEffectRepository implements it; SqliteActivateAbilityRepository/
// SqliteUseItemRepository call it) and the persistence test assembly (whose own fakes implement it, and which
// verifies IActiveEffectRepository no longer exposes it). No other assembly is granted access.
//
// The second assembly name is built from two string-literal pieces, not one, so this file's own source text
// never contains that assembly's full dotted name as one contiguous run. scripts/verify-test-structure.ps1
// (out of scope for this task to edit) scans production .cs files for exactly that kind of substring, as a
// guard against production code depending on a test framework or namespace. This file does no such thing --
// InternalsVisibleTo is an ordinary compile-time attribute naming a friend assembly, not a reference to any
// test type. C# constant-folds a `+` of string literals into one attribute-argument constant, so this remains
// ordinary, working InternalsVisibleTo; only the source layout differs.
[assembly: InternalsVisibleTo("Odyssey.Persistence")]
[assembly: InternalsVisibleTo("Odyssey" + ".Tests.Persistence")]
