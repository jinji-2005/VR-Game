# Game Design Plan

本文档用于确认第一版 FPS demo 怎么做。默认方案先追求小、完整、可演示；分歧项用选择题确认。

## Default Design

第一版推荐做成 `VR/FPS Training Range`：

```text
玩家身份：训练场测试员或基地防守者
主要目标：在限定时间内击中不断出现的目标
核心循环：进入场景 -> 目标出现 -> 瞄准射击 -> 获得反馈和分数 -> 时间结束显示结果
目标平台：Unity Editor / Simulator 优先，后续再适配 Quest
单局时长：2-3 分钟
场景规模：一张小地图
核心武器：一把半自动能量枪或手枪
结束条件：倒计时结束，显示得分、命中率和评级
```

这个方案的优点是实现成本低、FPS 特征明确、容易录制 demo，也方便后续扩展成 VR 交互或 Quest 真机版本。

## Design Decisions

讨论时逐项选择。没有强烈分歧时使用推荐项。

### 1. Theme

- A. 未来训练靶场（推荐）：清晰、易做、资源需求少。
- B. 太空基地防守：更有氛围，但需要更多场景美术。
- C. 房间清理演习：更像任务制 FPS，但路径和目标布置更复杂。

### 2. Main Scene

- A. 小型靶场（推荐）：玩家在固定区域射击出现的目标。
- B. 单房间战斗区：玩家在房间内移动并清除目标。
- C. 走廊推进：更有流程感，但需要更多关卡设计。

### 3. Player Movement

- A. 固定站位 + 转向瞄准（推荐）：最适合第一版和 VR，风险最低。
- B. 小范围移动：更像 FPS，但要处理碰撞和晕动风险。
- C. 自由移动：体验更完整，但实现和调试成本最高。

### 4. Target Type

- A. 弹出式靶子（推荐）：实现简单，反馈清楚。
- B. 悬浮无人机：更有游戏感，需要基础移动逻辑。
- C. 简单敌人：表现更强，但需要生命值、动画或 AI。

### 5. Weapon

- A. 半自动手枪（推荐）：输入简单、节奏清晰。
- B. 激光步枪：视觉效果更强，可做连续射击。
- C. 霰弹枪：手感特别，但命中判定更复杂。

### 6. Shooting Model

- A. Raycast 命中（推荐）：最适合第一版 FPS。
- B. Projectile 子弹：更真实，但要处理速度、碰撞和轨迹。
- C. 混合方案：主武器 Raycast，特殊目标再用 Projectile。

### 7. Objective

- A. 限时得分（推荐）：2 分钟内尽量获得高分。
- B. 清除全部目标：完成感强，但节奏依赖目标数量。
- C. 生存防守：目标靠近会扣分或失败，更刺激但逻辑更多。

### 8. Feedback

- A. 命中特效 + 音效 + 分数 UI（推荐）：最小但完整。
- B. 加入连击倍率：提升可玩性，需要额外 UI。
- C. 加入评级系统：适合 demo 展示，例如 S/A/B/C。

### 9. VR Interaction

- A. 先用鼠标键盘验证，后接 XR Device Simulator（推荐）。
- B. 一开始使用 XR Device Simulator。
- C. 一开始面向 Quest 真机。

### 10. Art Style

- A. 简洁科幻训练场（推荐）：低成本、统一、适合 demo。
- B. 写实军事风：素材依赖高，制作成本高。
- C. 卡通低多边形：轻量，但需要统一美术风格。

## Minimum Demo Scope

第一版只做这些：

- 一个可打开的主场景。
- 一个玩家视角。
- 一把可射击武器。
- 一类可命中的目标。
- 命中音效或视觉反馈。
- 分数、时间和结束界面。
- 一次完整的 2-3 分钟游玩流程。

第一版不做这些：

- 多关卡。
- 多武器切换。
- 复杂敌人 AI。
- 剧情系统。
- 背包、升级或商店。
- 多人联机。

## Acceptance Criteria

- 新环境 clone 后能在 Unity 打开。
- Console 无关键 Error。
- Play 后 10 秒内可以开始操作。
- 玩家能瞄准、射击并击中目标。
- 命中后有明确反馈。
- 单局结束后能看到结果。
- Demo 可录制成 1-2 分钟视频。

## Meeting Output

讨论结束后，将结果写入 `Docs/PROJECT.md`：

```text
Game Name:
One Sentence Pitch:
Theme:
Target Platform:
Core Loop:
Player Movement:
Weapon:
Target Type:
Objective:
Feedback:
Main Scene:
Win / End Condition:
Must Have:
Nice To Have:
Out Of Scope:
```

确认 `Docs/PROJECT.md` 后，再把第一轮任务写入 `Docs/TODO.md`。
