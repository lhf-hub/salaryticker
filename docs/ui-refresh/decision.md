# UI 清洗升级 · 决策记录

日期：2026-10-18
分支：`experiment/ui-refresh`（从 `main` @ `2fa85c1` 创建）

## 为什么开分支

用户认为当前界面「AI 味厚」（从 UI 到部分系统提示），希望在实验分支做一次从桌面端到官网的逐步升级；担心效果一般，所以先看方向稿再动正式界面。

`CLAUDE.md` 的默认规范是「只有 `main` 一个分支」；本次是用户明确批准的临时例外。实验期间不发布、不部署、不打 tag、不升版本；不满意可整支丢弃。合并后用 `git revert -m 1 <merge-sha>` 回退，本地分支保留到下一次发布。

## 用户已确认的选择

- 先出 3 个方向稿（含完整 Dashboard + 迷你窗），选 1 个整方向，最多再混 2 个轴；两轮修改上限。
- 第三个方向改为「浅色专注单板」，不做暗色主题。
- 文案范围：应用内状态与提示、启动页与设置页说明、版本日志的**新语气规范**；官网营销文案本轮不改。
- 已发布版本历史（`tools/release-notes.txt` / `tools/version-history.json`）冻结，不重写。
- 安装并使用 `frontend-design`、`web-design-guidelines`、`Impeccable`。

## Skill 安装记录（2026-10-18）

| Skill | 版本 / 来源 | 安装位置 | 卸载 |
|---|---|---|---|
| frontend-design | `ea10b213686c`，`claude-plugins-official` | user scope | `claude plugin uninstall frontend-design@claude-plugins-official` |
| web-design-guidelines | `vercel-labs/agent-skills` | `~/.claude/skills/web-design-guidelines` | `npx skills remove web-design-guidelines -g` |
| impeccable | `4.6.0`，`pbakaus/impeccable` | `~/.claude/skills/impeccable` | 删除该目录 |

说明：`npx impeccable install` 的引擎下载被网络拦截，改用 GitHub codeload zip 解压后复制 `.claude/skills/impeccable`；首次运行 launcher 仍可能尝试下载引擎，失败时直接读 `SKILL.md` 与 `reference/`。
未安装项目级 hook；仓库不提交 `.claude/`。

## Phase 0 基线

- `dotnet build`：通过。
- `dotnet test`：**231 通过 / 0 失败**（原 230 + 新增 `XamlResourceTests`）。
- `dotnet run -- smoke`：Main / Settings / Splash / Mini / Confirm / Gallery 全部 OK。
- 基线截图：`D:\Data\Coding\demo\_ui-baseline\`（main / settings / mini / splash / gallery，100% DPI）。
- 截图工具：`tools/capwin.ps1`（枚举进程可见顶层窗口，取面积最大者）。
- 起始 HEAD：`2fa85c1`。

## 文案清单

见 [copy-inventory.md](copy-inventory.md)。
