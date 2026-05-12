# TODO

## Current Goal

当前优先完成 `Level 0` 教学关卡，并把项目推进到“可进入、可移动、可找钥匙、可开门、可进入下一层”的状态。

## Priority Order

### P0. Project Baseline

- [ ] 确认 `backroom.unity` 是否作为第一层主场景，若确定则重命名到更清晰的场景名，例如 `Level0_Tutorial.unity`
- [ ] 将第一层场景加入 `EditorBuildSettings`
- [ ] 统一项目命名规则：场景、Prefab、材质、脚本命名都使用一致前缀
- [ ] 清理第一层场景中无用测试物体和无效引用
- [ ] 确认第一层的入口区、钥匙区、出口区三段流程已经在场景结构上存在

### P1. Level 0 Playable Loop

- [ ] 实现第一人称移动和视角控制
- [ ] 实现基础交互检测，例如中心点 Raycast + `E` 键交互
- [ ] 实现钥匙拾取逻辑
- [ ] 实现门锁逻辑：未拿钥匙无法通过，拿到钥匙后可开门
- [ ] 实现从第一层进入第二层的过渡触发
- [ ] 在第一层加入基础提示，例如“按 E 交互”或“需要钥匙”

### P2. Level 0 Atmosphere

- [ ] 调整灯光、走廊重复模块和空间节奏，让第一层更像教学关卡而不是纯迷宫
- [ ] 补充基础环境音，例如荧光灯嗡鸣、低频环境噪音、远处回响
- [ ] 调整钥匙和出口门的可识别度，避免玩家完全找不到目标
- [ ] 控制第一层总时长，目标是首次游玩 2-4 分钟内能通关

### P3. Transition To Layer 2

- [ ] 确认第二层最终选用哪个候选层级
- [ ] 确认第二层的两个结局形式
- [ ] 确认第一层结束后如何过渡到第二层
- [ ] 根据第二层方案补写 `Docs/PROJECT.md`

## Deliverable Standard For Level 0

- [ ] Unity 中能打开主场景并正常进入 Play
- [ ] 玩家能完成一次“探索 -> 找钥匙 -> 开门 -> 进入下一层”的完整流程
- [ ] Console 无红色 Error
- [ ] 场景空间不大，关键路径清晰
- [ ] 无实体、无战斗、无复杂多步骤谜题

## Four-Person Plan

### Member 1: Gameplay / Interaction

责任范围：

- 第一人称移动
- 视角控制
- 交互检测
- 钥匙拾取
- 门锁与开门逻辑

第一阶段任务：

- [ ] 建立 `PlayerController`
- [ ] 建立 `PlayerInteractor`
- [ ] 建立 `PickupItem`
- [ ] 建立 `LockedDoor`

依赖关系：

- 需要 Member 2 提供稳定场景结构和交互物体摆放点
- 需要 Member 4 协助做 Play 测试和问题回归

### Member 2: Level 0 Scene / Modular Environment

责任范围：

- 第一层空间布局
- 墙、地板、门、灯的重复模块搭建
- 钥匙区与出口区布置
- 过场门或出口门位置确认

第一阶段任务：

- [ ] 把当前 `backroom` 场景整理为教学关卡结构
- [ ] 划分入口区、探索区、钥匙区、出口区
- [ ] 优化路径长度，避免空间过大
- [ ] 统一 Backrooms 模块摆放和命名

依赖关系：

- 需要 Member 1 告知门、钥匙、交互点的脚本挂载需求
- 需要 Member 3 后续补灯光和氛围

### Member 3: Atmosphere / Audio / Presentation

责任范围：

- 灯光氛围
- 环境音
- 视觉识别度
- 基础 UI 提示

第一阶段任务：

- [ ] 调整荧光灯亮度和冷暖关系
- [ ] 增加环境底噪和循环音
- [ ] 为钥匙和出口提供更清晰的视觉区分
- [ ] 增加基础提示文本或过场提示

依赖关系：

- 需要 Member 2 提供稳定的场景布局
- 需要 Member 1 提供交互状态，决定什么时候显示提示

### Member 4: Integration / Docs / QA

责任范围：

- 文档维护
- 场景集成检查
- 测试与回归
- 第二层方案整理

第一阶段任务：

- [ ] 维护 `Docs/PROJECT.md`
- [ ] 维护 `Docs/TODO.md`
- [ ] 检查 Unity Console、缺失引用、脚本挂载状态
- [ ] 记录第一层试玩问题
- [ ] 组织第二层和双结局讨论

依赖关系：

- 需要读取 Member 1 / 2 / 3 的实际进度做整合
- 在每轮集成后负责确认是否达到可演示状态

## Collaboration Rules For This Sprint

- [ ] 场景主文件尽量只由一人主改，其他人优先改 Prefab 或脚本
- [ ] 新脚本统一放在 `Assets/_Project/Scripts/`
- [ ] 新 Prefab 统一放在 `Assets/_Project/Prefabs/Backrooms/`
- [ ] 重要对象命名清晰，例如 `Level0_ExitDoor`、`Level0_Keycard`
- [ ] 每次提交前确认 `.meta` 文件是否一起提交
- [ ] 每次合并后至少做一次完整通关测试

## Next Decision Blockers

以下内容会影响第二轮开发，需尽快确认：

- [ ] 第二层具体层级
- [ ] 第二层双结局形式
- [ ] 第二层是否引入实体或纯环境恐怖
- [ ] 第二层主谜题类型
