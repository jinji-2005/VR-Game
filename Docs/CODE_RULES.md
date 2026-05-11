# Code Rules

本文件是团队成员和 code agent 修改 Unity 代码时必须遵守的规则。规则的目标是减少冲突、保持项目结构稳定，并让多人协作时每次修改都容易检查。

## General

- 优先保持现有目录结构和命名风格。
- 修改前先阅读 `README.md`、`Docs/UNITY_WORKFLOW.md`、`Docs/ASSET_MANAGEMENT.md`、本文件，以及相关代码。
- 每次提交聚焦一个明确目标，避免把重构、资源导入、玩法修改混在一起。
- 不提交 `Library/`、`Temp/`、`Obj/`、`Build/`、`Builds/`、`Logs/`、`UserSettings/`。
- 不手动修改 `.meta` 文件里的 GUID。

## Unity Project Settings

Unity Editor 中保持以下设置：

- Version Control: Visible Meta Files
- Asset Serialization: Force Text

如果这些设置因为 Unity 版本或模板不同而变化，需要在 PR 或 commit 描述中说明。

## C# Style

- 类型、方法、属性使用 PascalCase。
- 局部变量和参数使用 camelCase。
- 私有序列化字段使用 `[SerializeField] private`。
- Unity 生命周期方法按常见执行顺序组织：`Awake`、`OnEnable`、`Start`、`Update`、`FixedUpdate`、`LateUpdate`、`OnDisable`、`OnDestroy`。
- 单个脚本只负责一个清晰职责，例如移动、射击、生命值、计分、交互输入。
- 避免在 `Update` 中反复调用昂贵查找，例如 `FindObjectOfType`、`GameObject.Find`。
- 需要调试输出时使用清晰前缀，并在提交前删除无用日志。

## Unity Assets

- 项目自有资源放在 `Assets/_Project/` 下。
- 第三方资源放在 `Assets/ThirdParty/` 或资源包自带目录，避免混进项目脚本目录。
- 场景放在 `Assets/_Project/Scenes/`。
- Prefab 放在 `Assets/_Project/Prefabs/`。
- 脚本放在 `Assets/_Project/Scripts/`。
- 材质放在 `Assets/_Project/Materials/`。
- 音频放在 `Assets/_Project/Audio/`。
- 美术资源放在 `Assets/_Project/Art/`。
- 项目设置类资源放在 `Assets/_Project/Settings/`。

## Scene And Prefab Safety

- 修改场景前先 `git pull`，降低 `.unity` 文件冲突概率。
- 多人不要同时修改同一个主场景；需要并行开发时拆分测试场景或 prefab。
- Prefab 修改后确认关联 `.meta` 文件一起提交。
- 大量重命名、移动资源前先和团队同步，因为这会影响引用关系和冲突概率。

## Review Checklist

- Unity Console 是否没有红色 Error。
- Play 模式是否能正常进入场景。
- 修改是否只影响本次任务范围。
- 是否遗漏 `.meta` 文件。
- 是否误提交本地缓存、构建产物或个人 IDE 配置。
