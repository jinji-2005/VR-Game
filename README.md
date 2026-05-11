# VR-Game

Unity VR FPS playable demo for AI3618 2025-2026 Spring.

本仓库用于开发一个小规模、可运行、可演示的 Unity VR/FPS demo。当前阶段只维护项目结构、协作规则和 Unity 同步流程；核心玩法、关卡目标和任务列表将在方案确认后补充。

## Project Direction

- Engine: Unity
- Genre: VR / FPS
- First target: Simulator / Unity Editor
- Later target: Quest device, if schedule and device access allow

## Structure

```text
Assets/           Unity project assets, scenes, scripts, prefabs, materials, audio, art
Packages/         Unity package manifest and lock file
ProjectSettings/  Unity project settings
Docs/             Team docs, workflow rules, code rules, milestones
```

Unity 生成的 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/` 不进入 Git。

## First Setup

1. Clone this repository.
2. Install Git LFS: `git lfs install`.
3. Pull LFS assets when needed: `git lfs pull`.
4. Open the repository root folder in Unity Hub.
5. Wait for Unity to generate `Library/`.
6. Open the main scene when it exists, then press Play.

## Docs

- [Development Guide](Docs/DEVELOPMENT_GUIDE.md)
- [Unity Project Guide](Docs/UNITY_PROJECT_GUIDE.md)
- [Game Design Plan](Docs/GAME_DESIGN_PLAN.md)
- [Milestones](Docs/MILESTONES.md)
- [Project](Docs/PROJECT.md)
- [TODO](Docs/TODO.md)

`Docs/PROJECT.md` and `Docs/TODO.md` are intentionally blank until the game design is confirmed.
