# VR-Game

Unity first-person Backrooms horror exploration project for AI3618 2025-2026 Spring.

本项目是一个以后室为主题的第一人称恐怖探索游戏 Demo，当前重点围绕场景氛围塑造、实体追逐、层级切换，以及可在 Unity Editor 中直接演示的交互体验展开。项目同时保留了 XR Interaction Toolkit 相关结构，方便后续继续适配 VR 设备。

## Game Overview

- **Game type**: First-person horror / Backrooms-like exploration
- **Core experience**: Explore unfamiliar spaces, search for progression clues, avoid hostile entities, and survive scene transitions between layers
- **Current platform**: Unity Editor desktop preview
- **Extension direction**: Keep the project structure compatible with later XR / VR adaptation

## Current Playable Content

### Level 0 - "Threshold"

Level 0 focuses on classic Backrooms atmosphere: yellow walls, fluorescent ceiling lights, repeating corridors, and oppressive empty rooms. The player explores the maze-like interior, searches for interactive progression objects, and deals with hostile entity pressure inside enclosed spaces.

Current gameplay in this level includes:

- free exploration in a multi-room indoor layout
- interaction with door / progression triggers
- hostile Bacteria entity patrol and chase behavior
- transition setup from Level 0 into the next layer

### Level 45 - "High-Altitude Ruins"

Level 45 shifts the experience from enclosed maze pressure to open high-altitude danger. The scene is built around broken roads, floating wreckage, fragmented structures, and large void areas, encouraging careful movement and route judgment.

Current gameplay in this level includes:

- fall transition arrival from Level 0
- high-altitude traversal across ruined structures
- void fall detection and delayed respawn
- checkpoint-based recovery to reduce repeated restart frustration
- desktop preview and XR-related runtime support kept in the same project flow

## Project Structure

The main game-specific content is organized under `Assets/_Project/`.

```text
Assets/
  _Project/
    Art/           Project-owned art assets and theme-specific visuals
    Audio/         BGM and SFX used by gameplay and transitions
    Materials/     Materials used by project scenes and prefabs
    Prefabs/       Reusable environment pieces, props, and gameplay prefabs
    Scenes/        Main playable scenes and their baked lighting data
    Scripts/       Runtime scripts and editor tooling for this project
    Settings/      Render pipeline and project-level runtime settings
  _Imported/       Third-party or externally imported raw assets kept before project reorganization
  Samples/         Unity or package sample content
  XR/ XRI/         XR Interaction Toolkit related assets and support content
Packages/          Unity package manifest and lock file
ProjectSettings/   Unity project settings tracked in Git
Docs/              Project notes and team documentation
```

## Asset and File Management

为避免 Unity 项目越做越乱，当前建议按下面的方式管理资源：

### 1. `Assets/_Project/` 只放项目正式使用的内容

- 场景、正式脚本、正式材质、正式音效、正式 Prefab 都放在这里
- 如果某个资源已经进入实际场景或被脚本引用，尽量整理到 `_Project/` 下对应目录

### 2. `Assets/_Imported/` 用来放刚导入、还没整理的外部资源

- 从 Unity Package、Asset Store、itch.io、Sketchfab 等来源导入的资源，先放这里
- 清理材质、检查贴图、修正 Shader、拆分 Prefab 后，再移动到 `_Project/`

### 3. 场景资源尽量按层级分组

- `Scenes/Level0/` 保存 Level 0 的光照、Volume 等场景附属文件
- `Scenes/Level45/` 保存 Level 45 的相关场景文件
- 不同层级尽量使用各自独立的材质、光照数据和 Prefab 组织方式，方便协作和排错

### 4. Prefab、材质、音频分开管理

- `Prefabs/` 里只放可复用对象，不把大量临时模型直接散放在场景目录
- `Materials/` 统一管理正式材质，避免同类材质在多个目录重复复制
- `Audio/BGM` 和 `Audio/SFX` 分开，方便查找和后续混音调整

### 5. Unity 协作时必须保留 `.meta`

- 所有进入 Git 的 Unity 资源文件都要连同对应 `.meta` 一起提交
- 不能只拷贝资源本体、不提交 `.meta`，否则引用关系、GUID 和场景绑定都可能丢失

### 6. 不提交 Unity 自动生成目录

以下目录由 Unity 本地生成，不应进入 Git：

- `Library/`
- `Temp/`
- `Obj/`
- `Logs/`
- `UserSettings/`

## Opening the Project

1. Clone this repository.
2. Install Git LFS: `git lfs install`
3. Pull LFS assets if needed: `git lfs pull`
4. Open the repository root in Unity Hub.
5. Wait for Unity to import assets and generate local cache folders.
6. Open `Assets/_Project/Scenes/Level0.unity` or `Assets/_Project/Scenes/Level45/Level45.unity` to preview gameplay.

## Related Docs

- [Development Guide](Docs/DEVELOPMENT_GUIDE.md)
- [Unity Project Guide](Docs/UNITY_PROJECT_GUIDE.md)
- [Game Design Plan](Docs/GAME_DESIGN_PLAN.md)
