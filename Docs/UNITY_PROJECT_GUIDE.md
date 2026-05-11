# Unity Project Guide

本文档说明 Unity 项目的打开、同步和资源管理规则。

## Project Files

Unity 项目根目录应包含：

```text
Assets/
Packages/
ProjectSettings/
```

当前仓库只有目录基线。创建真实 Unity 项目后，必须提交 `Packages/manifest.json`、`Packages/packages-lock.json` 和 `ProjectSettings/ProjectVersion.txt`。

## Open In Unity

1. 克隆仓库。
2. 执行 `git lfs install`。
3. 执行 `git lfs pull`。
4. 在 Unity Hub 中选择 `Add project from disk`。
5. 选择仓库根目录。
6. 等待 Unity 生成 `Library/`。
7. 打开主场景并点击 Play。

## Daily Sync

开始前：

```bash
git pull
git lfs pull
```

提交前：

```bash
git status
git add Assets Packages ProjectSettings Docs
git commit -m "type: describe change"
git push
```

只提交本次任务相关文件。

## Editor Settings

保持以下设置：

- Version Control: Visible Meta Files
- Asset Serialization: Force Text

## Asset Rules

必须进 Git：

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.meta`

不进 Git：

- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Builds/`
- `Logs/`
- `UserSettings/`
- build 产物
- demo 视频
- 未使用的大型素材包

普通 Git 适合：

- `.cs`
- `.unity`
- `.prefab`
- `.mat`
- `.asset`
- `.asmdef`
- `.json`
- `.md`
- `.meta`

Git LFS 适合：

- `.fbx`
- `.blend`
- `.obj`
- `.psd`
- `.tga`
- `.tif`
- `.wav`
- `.mp3`
- `.ogg`
- `.mp4`
- `.mov`
- `.zip`
- `.unitypackage`

## Size Guidelines

- 小于 10 MB：普通 Git 可以接受。
- 10-50 MB：模型、贴图、音频建议 Git LFS。
- 大于 50 MB：必须 Git LFS 或外部存储。
- 大于 200 MB：除非运行必需，否则不放仓库。

## Play Test

- Console 无红色 Error。
- Play 模式能进入主场景。
- 玩家移动、视角和基础交互可用。
- 手电筒、谜题目标、层级过渡或结局触发可用。
- 退出 Play 模式后场景、prefab、材质引用不丢失。
