---
name: write-cheap-model-prompts
description: Convert an approved implementation plan, code review, or page specification into direct, tightly scoped prompts for DeepSeek or GLM coding models. Use when the user wants a cheaper model to implement planned work; do not use when Codex should perform the implementation itself.
---

# Write Cheap-Model Implementation Prompts

Turn the supplied plan, review, specification, or finished design into copy-ready prompts for a less capable coding model.

The implementation prompt must be literal. Do not rely on the model to infer architecture, scope, dependencies, edge cases, or what “done” means.

## Choose the Target Model

Ask which model will execute the prompt only when the user has not identified it and the choice changes the task. Otherwise infer the target from the conversation. Put `Target model: DeepSeek` or `Target model: GLM` at the top of the generated prompt.

Use **DeepSeek Flash** for a small, mechanical change with clear files and acceptance checks. Use a stronger DeepSeek reasoning model for a tightly scoped bug whose cause must be traced across several files.

Use **GLM Flash** for a small UI or visually checked change, especially when screenshots are available. Use the full GLM coding model for a coherent multi-file feature or longer tool-driven task.

Do not assign either model a broad request such as “review and fix the project.” Split broad work into prompts that each have one outcome and one verification boundary. Recommend a stronger reviewing model after implementation when the change affects architecture, persistence, scene transitions, Unity lifecycle order, or several gameplay systems.

Model names vary by provider. Keep the prompt labeled by family unless the user gives an exact model name.

## Model-Specific Instructions

### DeepSeek

- Lead with the desired code change. Do not begin with background prose.
- Give exact files, symbols, signatures, and tests whenever they are known.
- Repeat critical prohibitions in `Do not do this`; do not rely on a single architecture reference to enforce them.
- Tell it to inspect the current implementation before editing and to preserve existing public behavior outside the stated change.
- Tell it not to add fallback logic, compatibility shims, duplicate state, or unrelated cleanup unless a numbered step requires it.
- Require evidence for every success claim. Compilation is not evidence that runtime behavior or a Unity PlayMode path passed.
- For reasoning models, request analysis of the root cause before edits, but require the final response to report results rather than a long chain of thought.

### GLM

- Give the complete file set and dependency order because the task may span several edits.
- State the architectural owner of every new rule or state value. Repeat which code remains in the Unity Shell.
- For UI or page work, provide the reference image, required states, interaction behavior, dimensions or layout rules, and a visual verification step.
- Tell it to inspect its rendered result or screenshot when visual tools are available and correct visible defects before finishing.
- Prevent speculative expansion: name the exact screens, components, systems, and states in scope.
- Add checkpoints for long tasks: inspect, implement Core, connect Shell, add tests, run verification, report.
- Require it to stop when a required file, tool, scene, or reference is unavailable rather than inventing a substitute.

For either family, use non-thinking or low effort for mechanical edits. Use high effort for daily multi-file coding. Reserve maximum effort for hard root-cause analysis or tightly coupled architectural work. Do not spend maximum effort on documentation-only or single-file formatting tasks.

## Before Writing

Read the relevant project files and instructions when they are available. Resolve facts before writing the prompt. Include exact file paths, type names, method names, data ownership, and existing tests that the implementing model must preserve.

Do not turn unresolved assumptions into implementation instructions. If a required fact cannot be verified, label it clearly and tell the implementing model how to inspect it before editing.

Preserve the user’s chosen design. Do not redesign the feature, expand its scope, add dependencies, or introduce unrelated cleanup.

## Prompt Structure

Write each prompt in this order:

1. **Target model** — DeepSeek or GLM, with the exact name if supplied.
2. **Objective** — one concrete outcome.
3. **Read first** — exact files or project instructions the model must inspect.
4. **Do this** — numbered implementation steps in dependency order.
5. **Do not do this** — explicit scope and behavior prohibitions.
6. **Required behavior** — observable behavior and edge cases.
7. **Verification** — exact builds, tests, visual checks, or manual checks to run.
8. **Stop and report if** — facts or unavailable tools that must halt implementation.
9. **Completion response** — what evidence the model must report.

Use the literal headings `Do this` and `Do not do this`.

## Writing Rules

- Start instructions with direct verbs: `Open`, `Add`, `Move`, `Replace`, `Call`, `Preserve`, `Run`, `Report`.
- Use short sentences. Put one action in each numbered step.
- Name the exact files and symbols to change.
- State which layer owns state and decisions when architecture boundaries matter.
- State what must remain in adapters, UI, or infrastructure code.
- Give exact enum values, events, commands, save fields, or method signatures when the plan defines them.
- Describe behavior with inputs and expected outputs.
- State how old saves, missing data, null services, full inventories, failed paths, or unavailable tools should behave when relevant.
- Tell the model to use existing project patterns instead of inventing parallel systems.
- Require the smallest coherent change that completes the objective.
- Require tests for new rules or state transitions when the project’s plan calls for them.
- Require the model to report tests it actually ran. It must not claim unavailable checks passed.
- Require the model to inspect the final diff and remove accidental edits before reporting completion.
- Tell the model not to commit, push, publish, or modify generated files unless the user requested it.

Avoid vague instructions such as:

- “Handle this properly.”
- “Make it robust.”
- “Fix any related issues.”
- “Use best practices.”
- “Add tests as needed.”
- “Refactor where appropriate.”

Replace vague language with a named behavior, file, constraint, or assertion.

## Scope the Work

Use one prompt when the changes form one tightly coupled implementation and must land together.

Use separate prompts when tasks can be implemented and verified independently. Order prompts by dependency. At the top of each later prompt, state which earlier prompt must already be complete.

Do not create tiny prompts for individual lines when one model needs the surrounding change to keep the code compiling.

## Failure and Stop Conditions

Tell the implementing model to stop and report evidence when:

- a named file or symbol does not exist;
- the current architecture contradicts the prompt;
- completing the task requires an unapproved dependency or destructive migration;
- a required test cannot run because its tool or service is unavailable;
- existing unrelated failures prevent verification.

Do not tell it to guess, silently substitute a different design, or claim success after compilation alone when runtime verification was required.

## Required Prompt Ending

End every implementation prompt with these requirements, adapted to the task:

```text
When finished:
1. List every file changed.
2. Explain the behavior that changed.
3. List every verification command or test actually run and its result.
4. List anything that could not be verified.
5. Do not claim the task is complete if a required check failed.
6. Do not commit or push unless explicitly instructed.
```

## Compact Example

```text
Target model
DeepSeek Flash

Objective
Move the player dash cooldown and charge rules into the existing engine-free Core so the 2D and 3D controllers use one implementation.

Read first
- Documents/ARCHITECTURE_GUARDRAILS.md
- Assets/Scripts/GamePresentation/Player/PlayerController2D.cs
- Assets/Scripts/GamePresentation/Player/PlayerController3D.cs
- Assets/Scripts/GameData/PlayerMovementConfig.cs

Do this
1. Add PlayerDashModel under Assets/Scripts/GameData.
2. Put dash charges, cooldown, recharge timing, dash duration, and start eligibility in that model.
3. Give the model elapsed time and dash requests as inputs.
4. Return explicit commands for starting, continuing, and stopping a dash.
5. Replace the duplicated rules in both controllers with calls to the same Core model.
6. Keep input reading, Rigidbody movement, and trail rendering in the controllers.
7. Add engine-free tests for cooldown, recharge, multiple charges, disabled dashing, and cancellation.

Do not do this
- Do not keep a second cooldown or charge counter in either controller.
- Do not move UnityEngine types into Game.Data.
- Do not change dash tuning values or player-facing movement behavior.
- Do not modify unrelated combat or travel code.

Required behavior
- Both controllers produce the same charge and cooldown behavior.
- Canceling a dash does not refund its charge.
- A disabled dash cannot start.

Verification
- Run the engine-free test suite.
- Build Game.Data and Game.Presentation.
- Run the relevant Unity tests if the Unity test service is available. Otherwise report that they were not run.

Stop and report if
- A named file or symbol does not exist.
- The existing architecture contradicts these instructions.
- The required tests cannot run.

When finished:
1. List every file changed.
2. Explain the behavior that changed.
3. List every verification command or test actually run and its result.
4. List anything that could not be verified.
5. Do not claim the task is complete if a required check failed.
6. Do not commit or push unless explicitly instructed.
```

Return the generated implementation prompt directly. Do not surround it with planning commentary unless the user asks for an explanation.
