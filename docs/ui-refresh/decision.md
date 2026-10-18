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

## 方向选择（2026-10-18）

用户选定 **B 流水台账** 作为主方向。

- 方向板：`docs/ui-refresh/directions.html`（A/B/C 三版完整 Dashboard + 迷你窗 + 组件）。
- 截图：`D:\Data\Coding\demo\_ui-baseline\direction-{a,b,c}.png`。
- B 的核心：中性纸、标签在左 / 金额在右、双线收总额、key-value 行、Segoe UI 等宽数字；绿色只做单一强调，红色只做负数/错误；不做假工资条家具。
- 下一步：WPF 保真 spike（Segoe UI 等宽数字、1px 线、focus、DPI），通过后再做 Dashboard 纵向切片。

## WPF 保真 spike（2026-10-18）

- B 方向可行：Segoe UI + `Typography.NumeralAlignment="Tabular"` 在 24px 样本下宽度差 **0.00 px**（`1111111111` vs `0000000000`）。
- **本机实际 DPI = 125%**（PixelsPerDip 1.25；物理屏幕 1920×1080，工作区 1920×1020，逻辑 1536×816），不是之前假设的 100%。`tools/capwin.ps1` 已改为 DPI 感知，后续截图是全分辨率；Phase 0 的旧基线截图是在未声明 DPI 感知时采集的（虚拟坐标），对比时以新截图为准。
- 1 DIP 规则线可渲染；focus ring 采用「Style 包 Control.Template」的写法，静态样张已验证；真实键盘焦点仍需在 Phase 2 用 Tab 实测。
- 1100×640 的 spike 在 125% 下仍超出逻辑工作区（约 691 DIP 高），说明 **FitToScreen / 降低最小高度是 Phase 2 的硬前置**。
- spike 窗口：`Views/DesignSpikeWindow.xaml`，启动 `dotnet run -- spike`；截图 `D:\Data\Coding\demo\_ui-baseline\spike.png`。

## Dashboard 纵向切片（2026-10-18）

- 主窗壳层 + Dashboard 已切到 B：去掉卡片堆叠、渐变、emoji 装饰，改为台账行、双线总额、进度刻度、状态点。
- 新增 `Resources/BTheme.xaml`（只加 key，旧界面不受影响）；`MainWindow` 与 `DashboardView` 使用 BTheme。
- 进度刻度：`SalarySnapshot.PayableDaysInMonth` + `MainViewModel.PayDayTicks`；格数 = 当月计薪天数，已填格数按进度向下取整。
- 文案：新增 `StatusShortText`、`MuyuStatusShortText`、`MuyuDisabledShortHint`；倒计时统一为「距上班/距下班」；`RemainingWorkDaysText` 改为「N 天」。
- `WindowFit`：按显示器工作区 ÷ 实际缩放限制窗口。主窗在 125% 下为 1180×768 DIPs（1475×960 物理），完整落在屏幕内；已接入 Main/Settings/Splash/Gallery。
- 截图：`D:\Data\Coding\demo\_ui-baseline\slice-main.png`。
- 待用户签字后，再铺设置页、弹窗、迷你窗与官网。

## 设置页切片（2026-10-18）

- `SettingsWindow` 用局部覆盖切到 B：卡片、标题、正文、按钮、左侧导航、TextBox、ComboBox、CheckBox、RadioButton。只影响设置窗口，不改全局样式。
- 关于页删除 emoji 标题与「写在最后」励志卡，免责只留一次；启动页文案改为「每次启动显示使用提示」；版本号改用墨色（不再用金额绿）。
- 左侧导航去掉 emoji，选中项用 1px 墨色左边线 + 浅底，不再用胶囊。
- 已知未迁移：政策页 2 个 `DatePicker` 及其日历弹层（下一轮补）。

## 摸鱼历史切片（2026-10-18）

- 三张汇总卡改为汇总表（今日/本周/本月 × 次数/时长/白赚）；历史表去掉卡片与逐行分隔，改 B 台账行。
- 幽默文案去 emoji，保留按真实工资折算的金额分档（不编造夸张数据）。
- 新增 `history` 启动参数，直接打开主窗并切到摸鱼历史，便于截图与目检。

## 弹窗 / 迷你窗 / 启动页（2026-10-18）

- ConfirmDialog：B 白卡、4px 圆角、墨/红按钮；补 `IsDefault`/`IsCancel` 与打开聚焦。
- MiniWindow：B 白卡 + 控制边界、墨色读数、状态去 emoji；透明度滑块改 B 样式；截图模式启动 ticker 并设满不透明度。
- SplashWindow：压到 880×460，三段说明（参数 / 隐私 / 免责），去掉 emoji、口号、「写在最后」与联系方式；保留「下次不再显示」与淡入。
- 新增 `confirm` 启动参数用于截图。
- 已知未迁移：主窗更新/提示条（条件显示）、设置页 2 个 DatePicker、样式画廊仍为旧设计系统。

## Phase 0 基线

- `dotnet build`：通过。
- `dotnet test`：**231 通过 / 0 失败**（原 230 + 新增 `XamlResourceTests`）。
- `dotnet run -- smoke`：Main / Settings / Splash / Mini / Confirm / Gallery 全部 OK。
- 基线截图：`D:\Data\Coding\demo\_ui-baseline\`（main / settings / mini / splash / gallery，100% DPI）。
- 截图工具：`tools/capwin.ps1`（枚举进程可见顶层窗口，取面积最大者）。
- 起始 HEAD：`2fa85c1`。

## 文案清单

见 [copy-inventory.md](copy-inventory.md)。
