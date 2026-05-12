# Project

## Overview

本项目是一个小规模、可运行、可演示的 Backrooms-like 第一人称恐怖探索 demo。

当前目标：

- 采用两层级结构。
- 第一层作为教学关卡。
- 第二层和双结局设计后续补充。

## Current Confirmed Direction

```text
Game Name: TBD
One Sentence Pitch: A short Backrooms-like first-person horror demo with two playable layers.
Reference Style: Backrooms-like exploration horror
Target Platform: Unity Editor / Simulator first
Playable Layer 1: Level 0 inspired tutorial layer
Playable Layer 2: TBD
```

## Playable Layer 1

### Reference

- Level choice: `Level 0`
- Source reference: [Backrooms Wiki CN - Level 0](https://backrooms-wiki-cn.wikidot.com/latest:level-0)
- Implementation direction: use the current `backroom` scene as the base visual and spatial style

### Purpose

第一层是教学关卡，目标不是制造最强恐怖压迫，而是让玩家快速理解：

- 如何移动和观察环境
- 如何寻找关键物
- 如何找到出口并进入下一层

### Core Gameplay

采用最简单的推进方式：

- 找到钥匙
- 用钥匙打开出口
- 成功进入下一关

这一层不加入复杂机制，不加入额外道具链，不加入实体追逐。

### Scope

- 场景空间不大，控制在短时间内可以走完
- 资产复用优先，墙面、地毯、灯光、门、走廊模块尽量重复使用
- 不需要额外功能型道具
- 不需要敌对实体
- 不需要战斗系统

### Player Experience Goals

- 玩家进入后立刻理解这是一个 Backrooms 风格空间
- 玩家能在短时间内完成一次完整的“观察 -> 找钥匙 -> 找出口 -> 进入下一层”流程
- 玩家不会因为地图太大或机制太复杂而卡住太久

### Required Elements

- 第一人称移动和视角控制
- 一个清晰可识别的钥匙物
- 一个需要钥匙才能通过的出口或门
- 基础环境音和灯光氛围
- 通往第二层的过渡触发

## Not In Layer 1

第一层暂不包含：

- 敌对实体
- 巡逻或追逐逻辑
- 复杂谜题链
- 多个钥匙物
- 电池、理智值、背包等资源系统
- 多结局分支

## Pending Decisions

以下内容后续补充：

- `Playable Layer 2`
- `Core Loop` for the full two-layer experience
- `Player Tool`
- `Main Puzzle`
- `Entity / Threat`
- `Ending A`
- `Ending B`
- `Must Have`
- `Nice To Have`
- `Out Of Scope`
