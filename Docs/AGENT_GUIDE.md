# Agent Guide

本文件给 code agent 使用。任何 agent 在修改本仓库前都必须遵守这些规则。

## Required Reading

修改前先阅读：

- `README.md`
- `Docs/CODE_RULES.md`
- `Docs/UNITY_WORKFLOW.md`
- `Docs/ASSET_MANAGEMENT.md`
- 与当前任务直接相关的脚本、场景说明或配置文件

`Docs/PROJECT.md` 和 `Docs/TODO.md` 当前可能为空，等团队完成核心玩法讨论后再作为主要产品上下文。

## Allowed Work

- 创建或修改 Unity C# 脚本。
- 补充文档和开发流程。
- 调整明确要求的项目配置。
- 创建测试用的独立脚本或说明文件。

## Forbidden Work

- 不提交 `Library/`、`Temp/`、`Obj/`、`Build/`、`Builds/`、`Logs/`、`UserSettings/`。
- 不手动修改 `.meta` GUID。
- 不批量移动或重命名 Unity 资源，除非用户明确要求。
- 不删除团队成员未说明要删除的资源、场景或 prefab。
- 不引入大型第三方资源包，除非用户明确确认。
- 不把 build 产物、demo 视频或课程 PDF 放入仓库，除非用户明确要求。

## Implementation Style

- 保持小步修改。
- 优先沿用现有目录和命名。
- 每个脚本职责清晰，避免巨型管理器脚本。
- 涉及输入、XR、物理、场景加载时，在说明中写清楚测试方式。

## Final Response Checklist

完成任务后说明：

- 修改了什么。
- 需要同学在 Unity 中如何验证。
- 是否运行过检查或测试。
- 是否存在 Git LFS、Unity 版本、缺少依赖等限制。
