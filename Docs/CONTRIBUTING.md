# Contributing

本仓库默认采用小步提交、清晰分支、先同步再修改的协作方式。

## Branches

- `main`: 稳定主分支，保存可打开、可继续开发的项目状态。
- `feature/<name>`: 新功能，例如 `feature/player-shooting`。
- `fix/<name>`: 修复问题，例如 `fix/input-binding`。
- `docs/<name>`: 文档修改，例如 `docs/unity-workflow`。
- `chore/<name>`: 项目配置、目录或工具类调整。

## Commits

推荐使用简短的 Conventional Commits 风格：

```text
docs: initialize Unity workflow
feat: add player shooting prototype
fix: repair target hit detection
chore: update Unity gitignore
```

## Daily Workflow

1. 开始前执行 `git pull`。
2. 在 Unity 中完成修改。
3. 检查 Unity Console。
4. 确认 `.meta` 文件是否需要一起提交。
5. 执行 `git status`，只提交本次任务相关文件。
6. 写清楚 commit message。
7. 推送分支并让至少一名同学检查。

## Pull Requests

- 标题说明本次改动目的。
- 描述中写清楚测试方式，例如是否在 Unity Editor 中进入 Play 模式。
- 如果修改场景、Prefab、ProjectSettings，需要特别说明。
- 不把玩法大改、资源导入、代码重构塞进同一个 PR。

## Conflict Handling

- 场景和 prefab 冲突优先找相关同学一起解决。
- 不随意删除对方新增资源。
- 不用 destructive git 命令回滚团队成员的改动。
