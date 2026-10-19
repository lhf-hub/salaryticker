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

## 官网切片（2026-10-18）

- 官网切到 B 台账：纸/墨/次/线/边界/账本绿/收据红；去掉卡片堆叠、渐变、阴影悬停、全大写 kicker、标题单词变色、箭头 CTA 与装饰 emoji。
- 功能卡片改为台账行；安装步骤保留真实 1/2/3 顺序；隐私、FAQ、更新日志、支持区改为规则线分隔。
- 真实示例截图：临时把 `settings.json` 改为税前 20000 的示例并截主窗/迷你窗，**截完已还原**；素材为 `server/assets/app-shot.*` 与 `mini-shot.*`，页面标注「演示数据」。
- 事实性修复：体积显示改为十进制 71.8 MB（对应 71,824,017 bytes）、迷你窗 242×96、离线表述改为「本地存储，不上传工资数据」、摸鱼引语改为示例、`donate.png` 404 修复、`og:image` 改绝对 URL。
- 可访问性：skip link、`:focus-visible`、弹窗焦点陷阱/关闭回焦/背景 `inert`、移动端 `<details>` 菜单、无 JS 时正文可见并有版本/赞赏兜底。
- 营销文案本轮未重写（按用户范围）；hero / FAQ / 摸鱼 / 页脚口号的重写列为后续建议。
- 本地预览使用 staged fixture（`server/` + `version.json` + `history.json`）；**未部署**。

## 官网 Apple 风重做（2026-10-19）

- 用户反馈 B 官网「太寡淡」，决定试一版 Apple 风格；范围仅官网，应用保持 B 不动。
- 视觉：`#F5F5F7` 灰白底 + 白色毛玻璃 + Apple 蓝 `#0071E3`（文字链接用 `#0066CC` 保证 AA）；居中大标题、发光应用窗口、非对称 bento、玻璃面板、低透明度冷暖光晕。
- 保留：营销文案、真实示例截图、`version.json`/`history.json` 逻辑、skip link、`:focus-visible`、弹窗焦点陷阱/回焦、移动端 `<details>`、无 JS 兜底、此前的事实性修正。
- 截图：`apple-hero.png` / `apple-full.png` / `apple-modal-*.png` / `apple-mobile*.png`（在 `D:\Data\Coding\demo\_ui-baseline\`）；本地 staged 预览，**未部署**。
- 若官网方向认可，再评估是否把这套浅色玻璃语言迁到 WPF；WPF 的真毛玻璃需要 DWM Acrylic/Mica，迁移成本另计。

## 官网 Apple 风加大力度（2026-10-19）

- 用户反馈第一版 Apple 风「再大胆一点」后追加：悬浮玻璃导航（非全宽、圆角胶囊）、玻璃 CTA Dock、首屏字号加到 88px、应用窗口更大且加发光/玻璃高光、功能区加入一张深色 bento 卡、「怎么算的」改为深色圆角对比带、摸鱼区暖色渐变加强、首屏淡入与迷你窗缓慢浮动。
- 导航玻璃从 .55 提高到 .86，导航链接用更深的 `#5E5E63`，避免滑过深色带时对比度掉到 AA 以下；文字链接仍用 `#0066CC`。
- 动效尊重 `prefers-reduced-motion`；截图 `apple-bold-hero.png` / `apple-bold-full.png` / `apple-bold-history.png` / `apple-bold-mobile.png`。
- 应用侧仍未改动；测试 231 全绿；未部署。

## 官网 GitHub 风暗色重做（2026-10-19）

- 用户认为浅色 Apple 版仍「太素、没有高级感」，且不如正式环境那版；要求多参考 GitHub / 逆向好样式。
- 参考 GitHub 当前设计语言：`#0d1117` canvas、纯黑 hero 带、单一暖色氛围光、中间字重、mono 版本号与代码窗口、绿色主操作、arctic blue 链接、pill 分段控件。
- 重做：暗色产品页；品牌珊瑚/琥珀光晕只出现在首屏产品窗后；公式改为 GitHub 风代码窗口；深色 bento + 彩色图标；绿色 CTA；真实应用截图为主角。保留文案、版本/历史、无障碍与无 JS 兜底。
- 截图：`github-hero.png` / `github-full.png` / `github-history.png` / `github-coffee.png` / `github-mobile.png`（`D:\Data\Coding\demo\_ui-baseline\`）。
- 应用不变；测试 231 全绿；未部署。

## 官网暖色高级版（2026-10-19）

- 用户认为 GitHub 暗色「太重」，要求保留 GitHub 的结构、回到正式环境的暖色。最终版：奶油 `#FAF7F2` 底、暖白卡片、珊瑚/琥珀单束氛围光、薄荷绿金额与深绿主操作。
- GitHub 结构保留：版本 mono pill、分段事实条、深色代码窗（红黄绿点 + `salary-rules.ts`）、非对称 bento、大标题中量字重、绿色 CTA、真实应用截图为主角。
- 页面节奏：暖色 hero + 单束光；功能 bento（珊瑚/暖白/浅米）；「怎么算的」深色代码窗 + 暖色说明；摸鱼暖色渐变面板；隐私/安装/FAQ/更新日志/支持为暖白卡片；页脚暖灰。
- 截图：`warm-hero.png` / `warm-full.png` / `warm-history.png` / `warm-coffee.png` / `warm-mobile.png`。
- 应用不变；测试 231 全绿；未部署。

## 官网字体层级与圆角（2026-10-19）

- 用户反馈：密度可以，但棱角不够圆润；全文统一字体太丑，需要明确的字体层级。
- 三层字体：**展示衬线** `Noto Serif SC`（H1/H2、品牌字、摸鱼引语）＋ **无衬线正文** `Noto Sans SC / 微软雅黑 / PingFang`＋ **等宽** `Cascadia Mono/Consolas`（版本、代码、readout 数据、步骤号、章节小标签）。
- 展示体自托管子集：`server/assets/noto-serif-sc-display.woff2`（257 KB，覆盖当前静态页面 660 个字符），许可见 `OFL-NotoSerifSC.txt`；标题改动后需用 fontTools 重新生成子集。
- 圆角：按钮/胶囊 980px，卡片 22px，代码窗 18px，弹窗 22px，迷你窗截图 14px，FAQ/更新日志/支持 22px；与正式环境的圆润感对齐。
- 章节增加 mono 小标签（features / how it works / muyu / privacy / install / faq / changelog / support）+ 彩色小点，和衬线 H2、无衬线正文形成三层。
- 截图：`type-hero.png` / `type-full.png` / `type-features-crop.png` / `type-how-crop.png` / `type-history.png` / `type-mobile.png`。
- 应用不变；测试 231 全绿；未部署。

## 官网色彩扩展（2026-10-19）

- 用户反馈「色彩少了」；在暖色高级版基础上加入六色柔和分区：珊瑚 / 琥珀 / 薄荷 / 雾蓝 / 玫瑰 / 青绿。
- 首屏多色氛围光（珊瑚+琥珀+薄荷+雾蓝）；bento 六张卡各分一色；「怎么算的」与 FAQ 用薄荷底；隐私用雾蓝底；支持区用暖桃底；安装三步分别绿/琥珀/雾蓝；页脚四色渐变收边；捐献按钮改深珊瑚。
- 字体层级、圆角、密度保持不变；所有文本对比度仍按 AA 选取。
- 截图：`color-hero.png` / `color-full.png` / `color-features-crop.png` / `color-support-crop.png` / `color-history.png` / `color-mobile.png`。
- 应用不变；测试 231 全绿；未部署。

## 官网色彩再加强（2026-10-19）

- 卡片与章节淡彩底再加深一档；bento 顶部加同色 2px 细线；H2 下加四色渐变短线；首屏多色洗底与产品窗光晕加强；facts 三段各带淡彩底；muyu 面板加强；页脚渐变线加粗到 3px；捐献按钮深珊瑚。
- 正文 `--muted` 加深到 `#665C52`，保证在加深后的淡彩底上仍达 AA；修复 `var(--mono)` → `var(--font-mono)`，等宽层真正生效。
- 字体层级、圆角、密度不变。
- 截图：`strong-hero.png` / `strong-full.png` / `strong-features-crop.png` / `strong-support-crop.png` / `strong-mobile.png`。
- 应用不变；测试 231 全绿；未部署。

## 官网丝滑动效（2026-10-19）

- 交互缓动：按钮/卡片/导航 hover 轻微位移与阴影过渡；卡片柔光跟随指针；首屏产品窗轻微跟随鼠标 3D 倾斜；版本圆点脉冲；氛围光 18s 缓慢漂移。
- 滚动体验：章节标题与卡片滚动渐入（由 JS 添加 `.reveal`，无 JS 不隐藏）；导航滚过顶部后出现柔和阴影。
- 弹层：确认/历史弹窗淡入 + 轻微缩放；FAQ 用 `::details-content` 做高度/透明度展开；移动端菜单淡入下滑。
- 全部遵循 `prefers-reduced-motion`：动画与过渡关闭，3D 倾斜不启用。
- 截图：`silk-hero.png` / `silk-full.png` / `silk-history.png` / `silk-mobile.png`（静态图无法体现动效，需实际交互体验）。
- 应用不变；测试 231 全绿；未部署。

## 卡片 3D 聚焦（2026-10-19）

- 鼠标悬停卡片时，卡片以 `perspective(900px)` 朝光标方向 `rotateX/rotateY`（约 ±4–5°）、`scale(1.02)`、上浮 4px，柔光继续跟随指针；离开回正。
- 触屏（`pointer: coarse`）与 `prefers-reduced-motion` 下不启用 3D/缩放，只保留基础悬停。
- 演示截图：`card-3d-hover-crop.png`（第三张卡片处于悬停态）。
- 应用不变；测试 231 全绿；未部署。

## Phase 0 基线

- `dotnet build`：通过。
- `dotnet test`：**231 通过 / 0 失败**（原 230 + 新增 `XamlResourceTests`）。
- `dotnet run -- smoke`：Main / Settings / Splash / Mini / Confirm / Gallery 全部 OK。
- 基线截图：`D:\Data\Coding\demo\_ui-baseline\`（main / settings / mini / splash / gallery，100% DPI）。
- 截图工具：`tools/capwin.ps1`（枚举进程可见顶层窗口，取面积最大者）。
- 起始 HEAD：`2fa85c1`。

## 文案清单

见 [copy-inventory.md](copy-inventory.md)。
