# Development Guide

本文档定义当前游戏方向、候选层级、可讨论选项和实现规则。当前方案是两层级的 Backrooms-like first-person horror demo。

## Default Game Direction

推荐默认方案：

```text
类型：第一人称恐怖探索
结构：两个可游玩的层级
起点：Level 0 风格的初始层级
第二层：优先讨论 Level 2 风格的工业隧道
核心循环：探索 -> 收集线索或钥匙物 -> 避开危险 -> 找到出口 -> 进入第二层 -> 做出结局选择
战斗定位：默认不做战斗系统，以探索、声音、灯光、追逐和资源压力制造恐怖
单局时长：5-8 分钟
目标平台：Unity Editor / Simulator 优先，Quest 后续评估
```

推荐原因：两层级结构足够完整，Level 0 的辨识度高，Level 2 的工业空间容易做出压迫感和分支结局，同时不需要复杂敌人 AI 或大型场景资产。

## Reference Boundary

Backrooms Wiki 可作为灵感来源，但项目应优先做原创化表达。

- 不直接复制 Wiki 长文本。
- 不直接使用 Wiki 图片或音频，除非确认授权并保留署名。
- 如使用 Wiki 的层级名称、设定或页面内容，应遵守来源页面的许可要求；Wiki 页脚标注为 Creative Commons Attribution-ShareAlike 3.0 License。
- 项目文档中保留参考链接，实际剧情、谜题、房间布局和资产尽量原创。

参考入口：

- Backrooms Wiki CN: https://backrooms-wiki-cn.wikidot.com/
- Normal Levels list: https://backrooms-wiki-cn.wikidot.com/normal-levels-i
- Level 0: https://backrooms-wiki-cn.wikidot.com/level-0
- Level 2: https://backrooms-wiki-cn.wikidot.com/level-2
- Level 4: https://backrooms-wiki-cn.wikidot.com/level-4
- Level 5: https://backrooms-wiki-cn.wikidot.com/level-5
- Level 6: https://backrooms-wiki-cn.wikidot.com/level-6
- Level 37: https://backrooms-wiki-cn.wikidot.com/level-37

## Candidate Level Pairs

### A. Level 0 -> Level 2（推荐）

- 核心体验：从黄色迷宫进入工业管道和维护走廊。
- 优点：空间好做、恐怖氛围强、双结局容易设计。
- 风险：需要做好音效、灯光和追逐节奏，否则容易变成普通走廊。
- 适合结局：正确启动电力门后逃离；错误进入深处后被困或触发坏结局。

### B. Level 0 -> Level 4

- 核心体验：从迷宫进入空旷办公室或办公楼层。
- 优点：资产简单，适合做钥匙、文件、终端和路线选择。
- 风险：恐怖感较弱，需要靠事件和声音弥补。
- 适合结局：找到出口电梯离开；误信错误指引进入未知区域。

### C. Level 0 -> Level 5

- 核心体验：从迷宫进入复古酒店或宴会厅。
- 优点：美术辨识度强，适合做剧情碎片和压迫氛围。
- 风险：对场景资产、灯光和装饰要求较高。
- 适合结局：完成仪式或解谜离开；留在酒店循环。

### D. Level 0 -> Level 6

- 核心体验：从明亮迷宫进入近乎全黑空间。
- 优点：素材成本低，适合用声音和心理压力制造恐怖。
- 风险：如果缺少引导，玩家容易迷路或无聊。
- 适合结局：跟随正确声音找到出口；跟随错误声音进入坏结局。

### E. Level 0 -> Level 37

- 核心体验：从干燥迷宫进入蓝色泳池空间。
- 优点：视觉反差大，适合做安静但诡异的恐怖。
- 风险：水体、反射、空间美术成本更高。
- 适合结局：找到正确泳池出口；潜入错误区域后失踪。

## Design Decisions

讨论时逐项选择。没有强烈分歧时使用推荐项。

### 1. Core Theme

- A. 迷失与逃离（推荐）：目标清楚，适合短 demo。
- B. 调查与记录：更强调文件、录音和环境叙事。
- C. 追逐与生存：更刺激，但需要敌人或危险系统。

### 2. Second Level

- A. Level 2 风格工业隧道（推荐）：压迫感强，实现成本适中。
- B. Level 4 风格办公室：更容易实现，但恐怖感较弱。
- C. Level 5 风格酒店：氛围强，但美术成本高。
- D. Level 6 风格黑暗空间：低资产成本，高音效和引导要求。
- E. Level 37 风格泳池空间：视觉独特，但水体实现成本高。

### 3. Gameplay Focus

- A. 探索 + 简单解谜（推荐）：最适合零经验项目。
- B. 探索 + 追逐：更有紧张感，需要实体和逃跑路线。
- C. 探索 + 资源管理：加入电池、理智值或氧气，但 UI 和数值更多。

### 4. Ending Design

- A. 第二层两个出口（推荐）：一个好结局，一个坏结局。
- B. 一个出口 + 一个隐藏条件：普通结局和真结局。
- C. 倒计时失败结局：超时触发坏结局。

### 5. Entity Design

- A. 无直接实体（推荐）：用声音、灯光、脚步和远处影子制造压力。
- B. 一个巡逻实体：实现简单追逐或躲避。
- C. 一个事件型实体：只在关键节点出现，降低 AI 难度。

### 6. Puzzle Design

- A. 三个钥匙物开门（推荐）：最直观，容易实现。
- B. 找密码开门：适合办公区或终端玩法。
- C. 修复电力系统：适合 Level 2 工业层级。
- D. 跟随声音或灯光：适合 Level 6，但需要好的引导。

### 7. Player Tools

- A. 手电筒（推荐）：恐怖游戏基础工具，能配合电池。
- B. 摄像机：适合录制、夜视和 UI 风格。
- C. 指南针或探测器：适合迷宫导航。

### 8. Failure Pressure

- A. 低压探索（推荐）：适合第一版，玩家主要体验氛围。
- B. 电池消耗：增加资源压力，但要控制难度。
- C. 实体追逐：刺激，但实现和调试成本更高。
- D. 理智值下降：氛围强，但反馈设计要清楚。

### 9. Level Transition

- A. 找到异常门进入第二层（推荐）：最容易表达。
- B. 掉入地面裂缝或 noclip：符合后室气质，但需要过场设计。
- C. 电梯或维修通道：适合 Level 2 或 Level 4。

### 10. Visual Style

- A. 低成本写实 + 程序化重复空间（推荐）：适合后室迷宫感。
- B. 低多边形恐怖：实现快，但风格需要统一。
- C. 高写实恐怖：效果好，但资源和性能压力高。

## Minimum Demo Scope

第一版必须包含：

- 两个可进入的层级。
- 第一人称移动和视角控制。
- 手电筒或等价照明工具。
- 至少一个简单解谜目标。
- 从 Level 0 到第二层的过渡。
- 第二层两个结局。
- 基础音效、环境氛围和结束界面。

第一版暂不做：

- 战斗系统。
- 多种实体。
- 大型开放地图。
- 背包系统。
- 复杂剧情分支。
- 多人联机。

## Meeting Output

讨论结束后，将结论写入 `Docs/PROJECT.md`：

```text
Game Name:
One Sentence Pitch:
Reference Style:
Target Platform:
Playable Layer 1:
Playable Layer 2:
Core Loop:
Player Tool:
Main Puzzle:
Entity / Threat:
Ending A:
Ending B:
Must Have:
Nice To Have:
Out Of Scope:
```

确认 `Docs/PROJECT.md` 后，再把第一轮任务写入 `Docs/TODO.md`。

## Agent Start Prompt

给 code agent 分配实现任务时，显式要求它先阅读必要文档：

```text
在开始实现前，请先阅读 README.md、Docs/DEVELOPMENT_GUIDE.md、Docs/UNITY_PROJECT_GUIDE.md、Docs/GAME_DESIGN_PLAN.md、Docs/PROJECT.md、Docs/TODO.md，以及本任务相关的代码和场景文件。遵守 Unity 资源、.meta 文件、Git LFS 和提交范围规则。完成后说明修改内容、验证方式和未验证项。
```

如果 `Docs/PROJECT.md` 或 `Docs/TODO.md` 仍为空，需要在任务描述中补充本次实现的具体目标。

## Git Workflow

- `main` 保持可打开、可继续开发。
- 功能分支使用 `feature/<name>`。
- 修复分支使用 `fix/<name>`。
- 文档分支使用 `docs/<name>`。
- 提交信息使用简短格式，例如 `feat: add flashlight prototype`。
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
- 单个脚本保持单一职责，例如移动、交互、手电筒、谜题、结局或音频触发。
- 避免在 `Update` 中反复调用 `FindObjectOfType`、`GameObject.Find` 等查找。

## Review Checklist

- Unity Console 无红色 Error。
- Play 模式能进入目标场景。
- Level 0 能过渡到第二层。
- 第二层两个结局都可触发。
- `.meta` 文件没有遗漏。
- 未提交本地缓存或构建产物。
- 修改范围与任务一致。
