# Unity GF Starter Implementation Plan

**Goal:** Deliver an independent Unity 2022.3 GF starter project under `D:\workspace\UnityGFStarter` with runnable examples and Chinese guidance.

**Architecture:** Vendor the paired official GF source at pinned revisions. Keep project policy, UI helpers and business examples in separate assemblies. Keep reusable threading and subscription ownership code independently testable.

**Spec:** `docs/design.md`

## Constraints

- Do not modify the source game, credentials, global dependencies or AGENTS.md.
- Do not delete existing files or directories.
- Preserve upstream MIT licenses and identify modifications.
- Target Unity 2022.3; store build outputs under `E:\AI\_DownLoads\unity-gf-starter`.

## Work items

- [x] Obtain official paired source archives and record pinned commit hashes.
- [x] Create a new destination and vendor source without compiled GF / compression binaries.
- [x] Write regression tests for queue, event scope, compression and wallet boundaries; observe missing-feature failure.
- [x] Implement those small modules and run the same tests.
- [x] Implement GF startup, UI layering, configuration, local settings and visible failure handling.
- [x] Generate Boot scene, example prefabs and local resource configuration from a safe editor menu.
- [x] Compile with the installed Unity editor and run EditMode / PlayMode verification.
- [x] Add an external Package / Windows player build entry and run a packaged player smoke check.
- [x] Write quickstart, architecture notes, thread rules, extension examples and validation record.
- [x] Inspect the final deliverable for source dependencies, missing scripts and license provenance.

## Verification commands

Run `tools/Verify-Core.ps1` for dependency-free C# regression tests. Run `tools/Verify-Unity.ps1` with the Unity editor path for actual project compilation, scene validation and test reports. See `docs/validation.md` for measured results.
