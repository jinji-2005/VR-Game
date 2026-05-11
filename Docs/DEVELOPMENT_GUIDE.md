# Development Guide

本文档定义协作、代码和 code agent 规则。目标是减少 Unity 资源冲突，保持提交可审查。

## Required Context

修改前阅读：

- `README.md`
- `Docs/UNITY_PROJECT_GUIDE.md`
- `Docs/DEVELOPMENT_GUIDE.md`
- `Docs/GAME_DESIGN_PLAN.md`
- `Docs/PROJECT.md`，如果已经填写
- `Docs/TODO.md`，如果已经填写
- 当前任务涉及的代码、场景或配置

`Docs/PROJECT.md` 和 `Docs/TODO.md` 当前保持空白，待游戏方案确认后补充。

## Agent Start Prompt

给 code agent 分配实现任务时，建议显式要求它先阅读必要文档。可直接使用：

```text
在开始实现前，请先阅读 README.md、Docs/DEVELOPMENT_GUIDE.md、Docs/UNITY_PROJECT_GUIDE.md、Docs/GAME_DESIGN_PLAN.md、Docs/PROJECT.md、Docs/TODO.md，以及本任务相关的代码和场景文件。遵守 Unity 资源、.meta 文件、Git LFS 和提交范围规则。完成后说明修改内容、验证方式和未验证项。
```

如果 `Docs/PROJECT.md` 或 `Docs/TODO.md` 仍为空，需要在任务描述中补充本次实现的具体目标。

## Git Workflow

- `main` 保持可打开、可继续开发。
- 功能分支使用 `feature/<name>`。
- 修复分支使用 `fix/<name>`。
- 文档分支使用 `docs/<name>`。
- 提交信息使用简短格式，例如 `feat: add player shooting prototype`。
- 每次提交只包含一个清晰目标。
- 提交前运行 `git status`，只暂存相关文件。

## Unity Safety Rules

- 必须提交 `Assets/`、`Packages/`、`ProjectSettings/` 中的有效项目文件。
- 必须提交 Unity 自动生成的 `.meta` 文件。
- 不提交 `Library/`、`Temp/`、`Obj/`、`Build/`、`Builds/`、`Logs/`、`UserSettings/`。
- 不手动修改 `.meta` 文件中的 GUID。
- 不批量移动、删除或重命名 Unity 资源，除非任务明确要求。
- 场景和 prefab 修改前先同步远端，降低冲突概率。

## C# Style

- 类型、方法、属性使用 PascalCase。
- 局部变量和参数使用 camelCase。
- 私有序列化字段使用 `[SerializeField] private`。
- Unity 生命周期方法按 `Awake`、`OnEnable`、`Start`、`Update`、`FixedUpdate`、`LateUpdate`、`OnDisable`、`OnDestroy` 排列。
- 单个脚本保持单一职责，例如移动、射击、生命值、计分或输入。
- 避免在 `Update` 中反复调用 `FindObjectOfType`、`GameObject.Find` 等查找。

## Code Agent Rules

- 修改范围必须贴合当前任务。
- 不删除未明确要求删除的资源、场景、prefab 或配置。
- 不引入大型第三方资源包，除非任务明确要求。
- 不提交 build 产物、demo 视频或课程 PDF。
- 完成后说明修改内容、验证方式和未验证项。

## Review Checklist

- Unity Console 无红色 Error。
- Play 模式能进入目标场景。
- `.meta` 文件没有遗漏。
- 未提交本地缓存或构建产物。
- 修改范围与任务一致。
