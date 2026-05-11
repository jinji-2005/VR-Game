# Game Design Plan

本文档用于在开发前确认游戏到底做什么。目标是确定一个小而完整的 playable demo。

## Output

讨论结束后，将结论写入 `Docs/PROJECT.md`：

```text
Game Name:
One Sentence Pitch:
Target Platform:
Core Loop:
Player Controls:
Main Scene:
Win / End Condition:
Must Have:
Nice To Have:
Out Of Scope:
```

## 1. Constraints

先确定边界：

- Unity 版本。
- 开发周期和提交日期。
- 是否只做 Simulator，或保留 Quest 真机目标。
- 可投入人数和每周可投入时间。
- 是否允许使用免费素材、Asset Store 资源或生成式资源。

默认建议：

- Simulator 优先。
- 一张小地图。
- 一个核心武器。
- 一个主要目标类型。
- 一条完整的 2-3 分钟游玩流程。

## 2. Core Experience

用三句话确认方向：

- 玩家是谁？
- 玩家要完成什么目标？
- 玩家为什么需要射击、移动或躲避？

输出格式：

```text
玩家扮演：
主要目标：
核心紧张感：
```

## 3. Minimum Demo

必须包含：

- 可进入的主场景。
- 可移动或可转向的玩家。
- 一种射击方式。
- 一个可被命中的目标。
- 一个反馈系统，例如音效、特效、分数或 UI。
- 一个结束条件，例如限时、击中数量或到达终点。

暂不做：

- 多关卡。
- 多武器。
- 复杂敌人 AI。
- 大型剧情。
- 复杂背包或成长系统。

## 4. Control Plan

按顺序验证：

1. Keyboard + Mouse prototype: 最快验证 FPS 核心循环。
2. XR Device Simulator: 验证 VR 交互雏形。
3. Quest device: 后期真机验证，需要额外配置和设备时间。

## 5. First Scene

只选一张小场景：

- 靶场：最容易完成射击、计分和反馈。
- 房间清理：可加入移动、搜索和击中目标。
- 防守点：可加入倒计时和目标刷新。

评估标准：

- 是否能在一周内做出可玩原型。
- 是否容易解释 demo 目标。
- 是否能展示 VR 交互价值。

## 6. Acceptance Criteria

- 新环境 clone 后能在 Unity 打开。
- Console 无关键 Error。
- 主场景 Play 后 10 秒内可开始操作。
- 玩家能完成一次完整流程。
- 游戏有明确开始、反馈和结束。
- Demo 可录制成 1-2 分钟视频。

## 7. First Sprint

确认方案后，只拆第一轮任务：

- 创建真实 Unity 项目。
- 确认 Unity 版本和项目设置。
- 创建主场景。
- 实现玩家基础控制。
- 实现基础射击。
- 实现目标命中反馈。
- 实现计分或结束条件。
- 录制一次内部验证视频。
