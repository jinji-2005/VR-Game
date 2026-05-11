# Asset Management

本文档说明 Unity 资源如何在 GitHub 中管理，避免仓库过大或资源引用丢失。

## Core Rule

同步 Unity 项目时，提交项目源文件，不提交本地缓存。

必须提交：

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.meta` 文件

不要提交：

- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Builds/`
- `Logs/`
- `UserSettings/`
- build 产物
- demo 视频
- 原始大型素材压缩包

## Git LFS

本仓库已经在 `.gitattributes` 中为常见大二进制资源配置 Git LFS 规则。

建议安装：

```bash
git lfs install
git lfs pull
```

推荐阈值：

- 小于 10 MB：普通 Git 可以接受。
- 10-50 MB：模型、贴图、音频建议 Git LFS。
- 大于 50 MB：不要普通 Git，必须 Git LFS 或外部网盘。
- 大于 200 MB：尽量不放仓库，除非是运行必须资源。

## File Type Guidance

适合 Git LFS：

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

普通 Git：

- `.cs`
- `.unity`
- `.prefab`
- `.mat`
- `.asset`
- `.asmdef`
- `.json`
- `.md`
- `.meta`

不要进仓库：

- 打包后的 `.apk`、`.aab`、`.exe`、`.app`
- Demo 视频
- Unity `Library/PackageCache/`
- 临时下载的完整素材包

## Meta Files

`.meta` 文件必须提交。它们保存 Unity 资源 GUID，缺失会导致场景、Prefab、材质、脚本引用丢失。

示例：

```text
Assets/_Project/Scenes/Main.unity
Assets/_Project/Scenes/Main.unity.meta
Assets/_Project/Scripts/PlayerController.cs
Assets/_Project/Scripts/PlayerController.cs.meta
```

## Importing Third Party Assets

- 小型资源可以直接导入 `Assets/ThirdParty/`。
- 大型资源包先由团队确认是否真的需要。
- 不把未使用的大素材整包提交进仓库。
- 如果资源来源有 license，保留 license 或来源说明。
