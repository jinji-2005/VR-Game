# VR-Game

Unity VR FPS playable demo for AI3618 2025-2026 Spring.

本仓库用于 4-6 人团队协作开发一个小规模、可运行、可演示的 Unity VR/FPS demo。当前阶段先完成项目初始化、团队规则、Unity 同步流程和 code agent 协作规范；核心玩法、关卡目标和任务列表会在团队讨论后补充。

## Current Direction

- Engine: Unity
- Genre: VR / FPS
- First target: Simulator / Unity Editor
- Later target: Quest device, if schedule and device access allow
- Documentation language: Chinese first, with key English technical terms kept

## Repository Structure

```text
Assets/           Unity project assets, scenes, scripts, prefabs, materials, audio, art
Packages/         Unity package manifest and lock file
ProjectSettings/  Unity project settings
Docs/             Team docs, workflow rules, code rules, milestones
```

Unity will generate local folders such as `Library/`, `Temp/`, `Obj/`, `Logs/`, and `UserSettings/`. These folders are intentionally ignored and should not be committed.

## Start Working

1. Clone this repository.
2. Install Git LFS before pulling large assets: `git lfs install`.
3. Pull LFS objects when the project starts using large assets: `git lfs pull`.
4. Open the repository root folder in Unity Hub.
5. Wait for Unity to generate the local `Library/` cache.
6. Open the main scene once it exists, then press Play in Unity Editor.

## Required Reading

- [Unity Workflow](Docs/UNITY_WORKFLOW.md)
- [Asset Management](Docs/ASSET_MANAGEMENT.md)
- [Code Rules](Docs/CODE_RULES.md)
- [Contributing](Docs/CONTRIBUTING.md)
- [Agent Guide](Docs/AGENT_GUIDE.md)
- [Milestones](Docs/MILESTONES.md)

`Docs/PROJECT.md` and `Docs/TODO.md` are intentionally left blank for now. Fill them after the team finishes the core game design discussion.
