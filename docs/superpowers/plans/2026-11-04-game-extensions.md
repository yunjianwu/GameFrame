# Game Extensions Implementation Plan

> Execute inline in the current session, one module at a time. The user has approved migration. Do not create branches, delete files, modify credentials or change the source game.

**Goal:** Deliver reusable tutorial, Spine, common UI and particle-budget modules in the existing starter, with runnable samples and Chinese documentation.

**Architecture:** Four isolated extension assemblies plus their editor/tests/sample assembly. UI and tutorial have no Spine or GF service dependency. Spine is bundled locally with its existing license headers.

**Tech Stack:** Unity 2022.3 LTS, Built-in, UGUI, C#, Spine 4.1.43 / spine-csharp 4.1.2, NUnit.

**Spec:** `docs/superpowers/specs/2026-11-04-game-extensions-design.md`

## Global Constraints

- Source `D:\MergeCookAndroid` remains read-only.
- Destination `D:\workspace\UnityGFStarter` contains all project source and docs.
- Build/log/test/intermediate output goes under `E:\AI\_DownLoads\unity-gf-starter`.
- No Lua, original gameplay classes, platform SDKs, DOTween Pro or global installation.
- Preserve existing starter behavior and assets; generators do not overwrite existing scenes.

## Task 1: Dependency integration and UI

- [x] Inventory and record source mapping. Copy the local Spine plugin with metadata and copyright headers, add explicit UGUI references.
- [x] Create `Assets/Extensions/UI/Runtime/Starter.Extensions.UI.asmdef` and editor assembly. Migrate independent controls with namespace `Starter.Extensions.UI`.
- [x] Replace Lua logging and resource services; expose `Func<string, Sprite>` on rich text. Preserve `<a>`, `<#...>`, `<quad>` and underline syntax.
- [x] Replace list DOTween calls with owned unscaled-time coroutine; cancel on disable and repeated init. Fix page table rebuild and list height calculation.
- [x] Add regression cases for button reset, Tab programmatic selection, shape geometry, rich text sprite resolver and list pooling.

## Task 2: Generic tutorial

- [x] Add `TutorialConfig`, `TutorialTarget`, `TutorialTargetRegistry`, `ITutorialProgressStore`, `TutorialRunner`, `TutorialMask`, `FingerPointer` under `Assets/Extensions/Tutorial/Runtime`.
- [x] Public flow: `Configure(config, registry, store)`, `Begin(resume)`, `Notify(signal)`, `SkipCurrent()`, `Stop()`; stable step IDs and explicit state/error.
- [x] Bind click targets only for the active step; wait/rebind on target availability; target wait timeout and unscaled delays; callbacks cannot advance stale steps.
- [x] Add tests for missing target timeout, late target after cancellation, repeated open, correct signal, save/resume and skip semantics.

## Task 3: Spine and animation sequences

- [x] Add `SpinePlayer`, `SpinePlayback`, `SpinePlaybackResult`, `SpineIntroLoop`, `SlotAlphaFollowerGraphic`, `SpineTransition`, `SpineSequence` under `Assets/Extensions/Spine/Runtime`; place the engine-independent `AnimationEventRelay` in UI so it does not require Spine.
- [x] Support graphic/world components, name validation, track/start offset distinction, queue, skin/speed/reverse/segment, completion/interruption/cancel cleanup.
- [x] Generate a minimal 4.1 skeleton from code for sample/test; do not copy game artwork.
- [x] Regression-test alpha once, animation replacement, manual cancellation, queue completion and missing animation fallback.

## Task 4: Effects

- [x] Migrate `ParticleSimulationBudgetManager` / `ParticleSystemController` under `Assets/Extensions/Effects/Runtime` with no Lua dependency.
- [x] Reuse scheduler buffers, correct register/unregister lifecycle, consistent scaled/unscaled clock, restore owned ParticleSystem state on disable.
- [x] Add fade helper and registration/re-enable coverage. Document soft-budget limitations.

## Task 5: Samples, docs, verification

- [x] Add `Assets/Extensions/Samples/Runtime/ExtensionsDemo.cs`, editor generator and `ExtensionGallery.unity`; preserve Boot as the default startup scene.
- [x] Gallery demonstrates click/drag/event tutorial, list reuse and pagination, common controls, generated Spine animation and particle budget.
- [x] Add Chinese `docs/extensions.md` and `docs/migration-map.md`; update README, third-party notices and validation report.
- [x] Run `tools/Verify-Core.ps1` and `tools/Verify-Unity.ps1`; build Windows64 through the existing builder. Inspect test XML and logs before reporting results.

## Review Follow-up

- [x] Reproduce and fix grouped-list refresh range, inline-tip pool binding, empty/variable-size/horizontal drag snapping, and Signal completion during presentation.
- [x] Reproduce and fix Spine replacement reentrancy through both the completion callback and public Finished event; keep internal release synchronous and dispatch user callbacks after subscribing the new handle.
- [x] Run the final complete suite: Core 16/16, EditMode 21/21, PlayMode 28/28; build a fresh Windows64 player and pass its Package startup smoke test. Exact paths and limits are recorded in `docs/validation.md`.
