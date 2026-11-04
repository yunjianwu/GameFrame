# Procedure Migration Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task in the existing isolated starter directory. User authorization already covers this migration; no Git repository exists here.

**Goal:** Deliver a runnable, reusable GF Procedure startup and scene transition flow.

**Architecture:** Keep Procedure orchestration in Starter.Game. Place startup settings, manifest validation and a resource update coordinator with a GF backend in Starter.Runtime. Upgrade only the necessary Boot registration and add separate GF business scenes.

**Tech Stack:** Unity 2022.3.62f2c1, GF v2021.05.31, C#, Unity Test Framework.

**Spec:** `docs/superpowers/specs/2026-11-04-procedure-migration-design.md`

## Global Constraints

- Original project is read-only. Do not delete files or change AGENTS, credentials or environment configuration.
- Preserve existing extension assets and scripts.
- No Git initialization or commits. All build outputs go to `E:\AI\_DownLoads\unity-gf-starter`.
- Editor and Package modes run offline. Updatable requires an explicit valid manifest endpoint.

### Task 1: Prove missing startup wiring

**Files:** Modify `Assets/Starter/Tests/PlayMode/BootFlowTests.cs`.

- [x] Assert real Menu/Play business scenes load before their UI, the previous scene unloads, and Boot survives. Literal scene paths let the regression compile before migration.
- [x] Run `tools/Verify-Unity.ps1 -Mode PlayMode`: 27 passed / 1 expected failure because Menu's business scene was missing (`20261104-182523-cde98b65`).

### Task 2: Implement and verify the resource startup boundary

**Files:** Create `Assets/Starter/Runtime/Startup/{StartupSettings,VersionManifest,ResourceUpdateCoordinator,GfResourceUpdateClient}.cs`; create shared resource regressions and their NUnit wrapper in `Assets/Starter/Tests/EditMode`.

**Interfaces:** `IResourceUpdateClient.Check(Action)`, `GetProgress(string[])`, `IsReady(string)`, `Update(string, Action<bool>)`, `Stop()`; `ResourceUpdateCoordinator.Begin()`, `Confirm()`, `Tick()`, `Cancel()`, `ReportFailure(string,int,int,string)`.

- [x] Test manifest rejection of missing endpoint/invalid lengths, confirmation before download, ordered resource groups, no progress after cancellation, failure without entering next group, and already-ready resources. Observed expected failures before implementing the coordinator and manifest validator.
- [x] Implement validation, one-shot update state machine, union progress, cancellation and official GF adapter.
- [x] Run shared core regressions: 30 passed / 0 failed / 0 compiler warnings or errors. Includes the review-driven last-resource failure regression, first observed failing.

### Task 3: Connect real Procedures and scenes

**Files:** Modify `GameBootstrap.cs`, `GameServices.cs`, all existing Game Procedures and `AssetNames.cs`. Create `ProcedureBase.cs`, `ProcedureLaunch.cs`, `ProcedureCheckVersion.cs`, `ProcedureUpdateResource.cs`, `ProcedureEnterGame.cs`, `ProcedureChangeScene.cs`. Modify `StarterProjectBuilder.cs` and `StarterBuildTools.cs`.

- [x] Boot binds services; Launch validates settings and initializes Package; CheckVersion fetches the manifest using a cancellable UnityWebRequest coroutine and checks/updates the GF version list.
- [x] UpdateResource owns the coordinator, confirmation, byte progress and subscriptions. Preload owns configuration only. EnterGame requests the first scene.
- [x] ChangeScene stops transient sound/entity work, waits for GF unloading, loads the requested business scene, and enters its UI procedure only after success. Each enter owns its request token and subscriptions.
- [x] Add a targeted `Starter/7. Configure Procedure Flow` migration, startup settings asset and two business scenes; preserve existing procedure names and scene objects. Validate registration as well as asset existence. Batch configuration succeeded (`procedure-migration/20261104-183811`).
- [x] Extend real Boot/UI and standalone smoke assertions to cover scene transitions. Real Boot regression passed (`20261104-183933-f5d4303d`, 1/1).

### Task 4: Verify and document delivery

**Files:** Update README and `docs/{procedures,quickstart,architecture,extension-guide,migration-map,validation}.md`.

- [x] Run `tools/Verify-Core.ps1` and `tools/Verify-Unity.ps1 -Mode All`: core 30/30, EditMode 35/35, PlayMode 28/28. Unity reports: `E:\AI\_DownLoads\unity-gf-starter\build\verification\20261104-184331-c777ff68`. EditMode wrote Passed XML but needed the script's documented process cleanup after 20 seconds; other stages exited normally.
- [x] Build Windows player using `StarterBuildTools.BuildWindowsPlayer`, then run `-starterSmokeTest`: exit 0 and `STARTER_PLAYER_SMOKE_PASSED` for full startup, Package resources, GF scene transitions, UI and events. Player: `E:\AI\_DownLoads\unity-gf-starter\build\packages\20261104-184618-a8e6beb2\player\UnityGFStarter.exe`; logs: `E:\AI\_DownLoads\unity-gf-starter\build\player-validation\20261104-184601-f0202afe`.
- [x] Perform the required read-only code review, fix the final-resource failure path that does not dispatch a GF group-completion callback, observe the new regression fail before the fix, and rerun core and full Unity verification. Follow-up review confirmed the Important finding resolved, with no new Critical/Important findings.
- [x] Confirm all 7 original Procedure SHA256 values match `E:\AI\_DownLoads\unity-gf-starter\build\migration\source-procedure-baseline.json`. Document real CDN updates, recovery/rollback, target devices and GPU rendering as unverified in `docs/validation.md`.
