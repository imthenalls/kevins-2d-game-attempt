# DeepSeek and GLM Coding Workflow

Use this guide to decide which model should implement a task and when Codex should review it. Model names and prices change, so choose by capability tier unless a provider gives an exact current name.

## Quick Choice

| Task | First choice | Effort | Review afterward |
|---|---|---:|---|
| Rename, small documentation edit, or simple test update | DeepSeek Flash | Low | Usually no |
| One-file bug with a known cause | DeepSeek Flash | Low | Review the diff |
| Bug requiring investigation across a few files | DeepSeek reasoning model | High | Yes |
| Small UI change with an existing screenshot or design | GLM Flash | High | Visually inspect |
| Multi-file UI or page implementation | Full GLM coding model | High | Yes, including screenshots |
| Engine-free Core model plus Unity Shell adapters | Full GLM or stronger DeepSeek model | High | Yes |
| Save/load, scene travel, Unity lifecycle, or persistence change | Stronger model | High or maximum | Always |
| Broad architecture migration or an unclear intermittent bug | Codex first | High | Codex should plan and review |

## Use DeepSeek For

DeepSeek is the default for a narrowly defined code change. It works best when the prompt names the exact files, symbols, expected behavior, and tests.

Good tasks:

- Fix one identified defect.
- Add tests for an existing behavior.
- Implement a small Core model from an approved design.
- Update save-data mapping when the schema and compatibility behavior are specified.
- Remove duplicated logic when the canonical owner is named.

Avoid giving it a general instruction such as “fix the NPC system.” First identify the defect and split the work into testable changes. Require it to list the tests it actually ran because a successful build does not verify Unity runtime behavior.

## Use GLM For

GLM is the default for a coherent feature that crosses several files or needs visual inspection. It works best when the prompt describes the dependency order and repeats which layer owns each rule.

Good tasks:

- Build a UI from a screenshot or finished specification.
- Connect several existing components into one feature.
- Implement a Core model, its Unity facade, and the related tests.
- Inspect a rendered page or screenshot and correct layout defects.
- Complete a longer task with clear checkpoints.

For visual work, supply the reference image and require a final screenshot comparison. State every required UI state, including empty, disabled, loading, failure, and full-inventory states when they apply.

## Use Codex For

Use Codex before implementation when the task is unclear, architectural, or risky. Use it after cheaper-model implementation when correctness depends on connections the compiler cannot prove.

Codex should handle or review:

- Architecture audits and migration plans.
- Root-cause analysis for intermittent behavior.
- Save/load compatibility and migration safety.
- Unity `Awake`, `OnEnable`, scene-load, and `DontDestroyOnLoad` ordering.
- Travel between 2D scenes and the 3D Town.
- Full gameplay sequences involving quests, rewards, inventory, persistence, and restart.
- Review of claims that tests passed or that architecture rules were followed.

## Recommended Loop

1. Ask Codex to inspect the project and produce a concrete implementation plan.
2. Ask Codex to generate a prompt with `write-cheap-model-prompts` and name DeepSeek or GLM as the target.
3. Give that prompt to the selected model without shortening it.
4. Return the model's report and diff to Codex for review.
5. If Codex finds a defect, generate one correction prompt for that defect.
6. Run the full project verification before accepting the change.

Do not send a cheaper model several unrelated fixes in one prompt. Complete and review one coherent change before starting the next.

## Prompt Checklist

Before sending a prompt, confirm it contains:

- One concrete objective.
- Exact files and symbols to inspect.
- Numbered `Do this` instructions.
- Explicit `Do not do this` constraints.
- Observable behavior and edge cases.
- Exact verification commands or manual checks.
- Conditions that require the model to stop and report.
- A required final list of changed files and actual test results.

## Unity Project Rules to Repeat

For this project, include these rules whenever they apply:

- `Game.Data` and the `Game.Core` namespace own authoritative state and decisions.
- `Game.Data` must not reference `UnityEngine`.
- `Game.Presentation` contains MonoBehaviours, Unity references, input, physics, rendering, and thin facades.
- Do not edit scenes or prefabs unless the task explicitly authorizes those assets.
- Preserve `.meta` files when moving Unity assets.
- Do not introduce `IReadOnlyList<T>`.
- Add the required XML summary and Unity setup instructions to every new C# script.
- Run `Tools/verify-all.ps1`, or report exactly which parts could not run.
- Do not claim Unity runtime verification from compilation alone.

## Current Official References

Check these pages before choosing a paid plan or relying on a specific model name:

- DeepSeek models and pricing: <https://api-docs.deepseek.com/quick_start/pricing/>
- DeepSeek API updates: <https://api-docs.deepseek.com/updates/>
- GLM-5.3: <https://z.ai/blog/glm-5.3>
- GLM-5.3-Flash: <https://z.ai/blog/glm-5.3-flash>
- ZCode model connection guide: <https://zcode.z.ai/en/docs/configuration>

Last reviewed: September 27, 2026.
