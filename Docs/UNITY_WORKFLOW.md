# Unity Workflow

本文档说明团队成员如何在本地打开、同步和测试 Unity 项目。

## What GitHub Syncs

GitHub 同步的是 Unity 项目的源文件和配置，不同步 Unity 本地缓存。

必须提交：

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- 所有 `.meta` 文件

不要提交：

- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Builds/`
- `Logs/`
- `UserSettings/`
- IDE 本地缓存
- 打包产物
- Demo 视频

## First Setup

1. 克隆仓库。
2. 安装 Git LFS。
3. 执行 `git lfs install`。
4. 执行 `git lfs pull`。
5. 打开 Unity Hub。
6. 选择 `Add project from disk`。
7. 选择仓库根目录 `VR-Game/`。
8. 等待 Unity 生成 `Library/`。

如果当前仓库还没有真实 `Packages/manifest.json` 和 `ProjectSettings/ProjectVersion.txt`，说明 Unity 工程还没有正式创建。第一个创建 Unity 项目的同学需要在仓库根目录创建项目，然后提交 `Assets/`、`Packages/`、`ProjectSettings/`。

## Daily Sync

开始工作前：

```bash
git pull
git lfs pull
```

完成工作后：

```bash
git status
git add Assets Packages ProjectSettings Docs
git commit -m "type: describe change"
git push
```

提交前确认没有 `Library/`、`Temp/`、`Obj/`、`Build/`、`Logs/`、`UserSettings/`。

## Unity Editor Settings

在 Unity 中保持：

- Version Control: Visible Meta Files
- Asset Serialization: Force Text

推荐在项目创建后尽早确认这些设置。

## Play Test

基础测试步骤：

1. 打开主场景。
2. 清空或检查 Console。
3. 点击 Play。
4. 确认没有红色 Error。
5. 确认玩家视角和移动可用。
6. 确认射击、命中反馈、计分或任务目标可用。
7. 退出 Play 模式。
8. 确认场景、Prefab、材质引用没有丢失。

## Simulator First

当前默认先支持 Unity Editor / Simulator。Quest 真机支持后续再做，届时需要补充 Android Build Support、OpenXR 或 Meta XR、设备开发者模式和真机打包流程。
