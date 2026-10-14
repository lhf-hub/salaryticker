# SalaryTicker 实时计算器

一个 Windows 桌面工资实时计算器（除"检查新版本"外完全离线）：输入税前/税后工资与班制，实时看到"本月已赚 / 今日已赚 / 当前小时已赚"金额增长，附带摸鱼计时、下班倒计时、迷你悬浮窗等功能。

> 🧭 **关于参数**：本工具内置深圳市的社保、公积金、个税参数（开箱即用）；其他城市可在「设置 → 政策参数」中按当地政策自行调整。有疑问或建议，欢迎联系 **Lee 老师**：leemail208@163.com
>
> 📌 计算结果仅供参考，以实际工资条为准。

---

## 功能特性

| 功能 | 说明 |
|---|---|
| **两种模式** | 税前模式（自动计算五险一金 + 累计预扣个税 → 税后到手）；税后模式（只填到手金额，直通实时计算） |
| **个税累计修正** | 累计预扣法默认假设每月工资相同；年中涨薪后可在「设置 → 个税参数」里**逐月录入本年度各月的实际税前收入**（留空的月份按当前月薪算），跨过税率档时个税才不会被多算 |
| **五险一金** | 养老/医疗/失业各支持**自动（用工资作基数）/ 按下限 / 按上限 / 自定义基数 / 直接填月缴额** 五种输入方式；医保一档/二档；公积金支持**金额 / 基数×比例 / 基数×比例+上限**三种方式 |
| **实时工资** | 日薪 = 税后月薪 ÷ **当月实际计薪天数**（按每周工作日与节假日/补班日自动算出，所以节假日多的月份日薪自动更高）；秒薪实时累计，金额按 月/天/小时 三维展示。**每月最后一个计薪日下班时，「本月已赚」正好等于税后月薪** |
| **当月补发** | 月中打开自动补发当月 1 号到昨天的工资（仅计薪日） |
| **班制** | 4 种预设（五天八小时双休 / 大小周 / 六天工作制 / 每日十小时）+ 全自定义（上下班时间、多段午休、每周工作日、大小周锚点） |
| **节假日与补班** | 内置 2026 年法定节假日表 + **调休补班日**表（均可编辑）：节假日强制休息不计薪；补班日即使是周末也正常上班计薪 |
| **摸鱼功能** | 摸鱼按钮计时 + 实时「白赚」金额（**只统计落在工作时段内的秒数**，与「今日已赚」同口径）。**非工作时段按钮直接禁用**并提示"这段时间是你自由支配的"；会话跨进午休或下班时会**自动结束**；摸鱼历史本地存储；今日/本周/本月统计；幽默文案 |
| **迷你悬浮窗** | 无边框置顶、可拖拽、透明度可调、位置记忆、双击返回主窗；状态行常驻**下班倒计时**（`赚钱中 💪 · 距下班 01:22:40`），不用切换指标就能看到还剩多久 |
| **下班提醒** | 到下班时间响铃约 60 秒（系统提示音），默认关闭，可在设置中打开；仅在应用运行期间有效 |
| **启动提示页** | 每次启动展示温馨提示（可勾选不再显示，设置中可恢复） |
| **离线可用** | 不使用数据库，全部数据存本地 JSON；只有"检查新版本"会发起网络请求，可在设置中关闭 |

## 技术栈

- C# / .NET 8 / WPF / MVVM（CommunityToolkit.Mvvm）
- 持久化：System.Text.Json → `%AppData%\SalaryTicker\`
- 无数据库；除可关闭的更新检查外无任何网络请求
- 设计系统：**暖阳浅色主题**（奶油白底 #FAF7F2 + 珊瑚橘主色 #E07A5F + 薄荷绿金额 #059669），全自定义 ControlTemplate（胶囊按钮 / 大圆角卡片 / 丝滑滚动条）

## 项目结构

```
SalaryTicker/
├── Models/          数据模型（AppSettings / ShiftSettings / MuyuRecord 等）
├── Services/        业务服务
│   ├── ConfigService.cs          配置持久化门面
│   ├── JsonFileStore.cs          泛型 JSON 存储（原子写 + 备份 + 损坏回退）
│   ├── SettingsDefaults.cs       默认值工厂（政策参数唯一入口）
│   ├── SocialSecurityCalculator.cs  五险一金计算
│   ├── TaxCalculator.cs          个税累计预扣法
│   ├── SalaryCalculator.cs       税后工资计算
│   ├── WorkTimeCalculator.cs     班制判定与状态机
│   ├── CountdownFormatter.cs     倒计时显示格式化（主界面与迷你窗共用）
│   ├── SalaryEngine.cs           实时快照计算（无状态重算）
│   ├── SalaryTickerService.cs    每秒 tick 服务
│   ├── MuyuSessionManager.cs     摸鱼会话管理
│   └── UpdateService.cs / UpdateLogic.cs   版本检查、下载校验、接力替换（唯一联网模块）
├── ViewModels/      MVVM 视图模型
├── Views/           窗口与页面（SplashWindow / MainWindow / SettingsWindow / MiniWindow）
├── Resources/       设计系统（Colors / Styles / Controls / Icons）
├── Converters/      值转换器
├── Dialogs/         暖色自定义对话框
├── Assets/          应用图标（.ico）
├── tools/           图标生成 / 一键发布脚本
├── server/          Ubuntu 部署文件（Nginx 配置 + 下载页 + 部署说明）
└── tests/           xUnit 单元测试（133 个用例）
```

## 数据存储

| 文件 | 内容 |
|---|---|
| `%AppData%\SalaryTicker\settings.json` | 用户配置（工资、五险一金、班制、政策参数、节假日） |
| `%AppData%\SalaryTicker\runtime.json` | 运行时状态（节流保存） |
| `%AppData%\SalaryTicker\muyu-history.json` | 摸鱼历史记录 |
| `%AppData%\SalaryTicker\backup\` | 自动备份（每类文件保留最近 10 份） |

**容错设计**：写入采用"先写临时文件再替换"；配置文件损坏时自动按时间戳回退最近备份并提示；无可用备份时重置为默认值，不会崩溃。

## 应用图标

图标为珊瑚橘渐变圆角方块 + 白色 ¥ 符号（与应用配色一致），已嵌入 exe 与所有窗口。

如需修改图标设计，编辑 `tools/make-icon.ps1` 中的绘制参数后重新生成：

```powershell
powershell -File tools/make-icon.ps1
```

脚本会输出 `Assets/SalaryTicker.ico`（多尺寸 16/24/32/48/64/128/256）与预览图。

## 构建与运行

```bash
# 开发运行
dotnet run

# 运行单元测试（133 个用例）
dotnet test

# 样式画廊（设计系统验收用）
dotnet run -- gallery
```

## 发布打包

```powershell
# 一键发布单文件 exe（自包含，无需目标机器安装 .NET）
powershell -ExecutionPolicy Bypass -File publish.ps1
```

或手动执行：

```bash
dotnet publish SalaryTicker.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

> **注意**：WPF **不支持** IL 裁剪（`PublishTrimmed=true` 会导致编译错误），因此发布命令中不含该参数。
> 自包含产物约 70–100 MB（含 .NET 运行时），双击即可运行，属正常大小。

## 服务端托管与自动更新

应用支持托管在自己的服务器上供用户下载，并提供**应用内一键更新**。

```
服务器 /var/www/salaryticker/
├── index.html                     下载页（自动读取 version.json 渲染版本与更新日志）
├── version.json                   版本清单：版本号 / 下载地址 / SHA256 / 更新说明
├── history.json                   版本历史清单（官网"历史版本"弹窗的数据源，由 deploy.ps1 维护）
├── SalaryTicker.exe -> releases/… 最新版软链
└── releases/SalaryTicker-<版本>.exe  历史版本归档

用户端：启动后异步 GET version.json（几百字节）→ 有新版则顶部提示 →
点「立即更新」→ 下载 + SHA256 校验 → 接力脚本在应用退出后替换 exe 并重启
```

**三个各司其职的一键发布脚本**（代码、应用、官网节奏不同，互不干扰）：

```powershell
notepad tools\release-notes.txt          # 1. 写本版更新日志
notepad SalaryTicker.csproj              # 2. 改 <Version>

# 发代码到 GitHub（测试 + 构建 + 提交 + tag + 推送 + 建 Release）
powershell -File tools\release.ps1

# 发应用到自己的服务器（复用上一步构建的同一个 exe，不重新构建）
powershell -File tools\deploy.ps1 -SkipPublish

# 发官网（index.html + assets/）
powershell -File tools\deploy-site.ps1   # 上传后会访问线上站点自动验证
```

| 脚本 | 发布内容 | 常用开关 |
|---|---|---|
| `tools\release.ps1` | 代码进 GitHub：测试 + 构建 + 提交 + tag + 推送 + 建 Release（附 exe） | `-NoTag` 日常推送不打 tag<br>`-SkipPush` 本地预演<br>`-SkipBuild` 复用已有构建<br>`-Message "<提交信息>"` |
| `tools\deploy.ps1` | exe 归档 + version.json + 下载软链 | `-SkipUpload` 只构建不上传<br>`-SkipPublish` 复用 release.ps1 的构建 |
| `tools\deploy-site.ps1` | index.html + assets/ | `-WithNginx` 同时更新 nginx 配置（本地预检 + 服务器上 `nginx -t` 不通过自动回滚）<br>`-NoVerify` 跳过上传后验证 |

> 源码托管在 GitHub（公开，Apache-2.0），每次发版会建一个 **Release** 并附上该版本的 exe，作为异地归档。但**应用的分发不依赖它**：应用内「检查更新」始终指向自己的服务器，exe 也只从服务器下载。`release.ps1` 构建一次，`deploy.ps1 -SkipPublish` 上传同一个文件，保证两边是**同一份二进制**。首次使用需把 `tools/github.config.sample.json` 复制为 `tools/github.config.json` 并填入 PAT（该文件已 gitignore）。

> 上传后验证会实际访问线上站点，并给出可操作的诊断（如「域名被 ICP 备案拦截」「HTTPS 端口不通」「nginx 根目录指错」）。

首次使用需把 `tools/deploy.config.sample.json` 复制为 `tools/deploy.config.json` 并填写服务器信息（该文件已 gitignore）。

**服务器端部署步骤见 [server/README.md](server/README.md)**（含 Nginx 配置、HTTPS 证书申请、验证命令与常见问题）。

**隐私与安全**：
- 应用**不上传任何数据**，只 GET 版本清单；下载安装包仅在用户点击「立即更新」后发生
- 更新检查可在 设置 → 更新 中**完全关闭**（关闭后应用不发起任何网络请求）
- 下载的安装包会与清单中的 SHA256 比对，校验失败则拒绝替换
- 建议使用 HTTPS（HTTP 下清单可被中间人替换）

> **重要提醒**：无代码签名证书时，Windows SmartScreen 会在首次运行新版本时提示"来源不明"，
> 用户需点「更多信息 → 仍要运行」。只有购买 Authenticode 代码签名证书才能消除该提示。
>
> 另：应用需放在**可写目录**（如 `D:\Apps\`）。放在 `Program Files` 下会因权限不足导致自动更新失败（应用会给出提示）。

## 政策参数说明

所有政策参数（社保基数上下限与比例、公积金比例、个税税率表、节假日）**均可在设置中修改**，无需更新程序：

- 社保参数：设置 → 政策参数
- 个税税率表：内置 7 级累进预扣率
- 节假日：内置 2026 年法定节假日（**连休安排为推定值，请以国务院办公厅通知为准**，可自行增删）

其他扣款（企业年金、补充医疗等）按**税后扣减**口径计入。

## 免责声明

- 本工具计算结果仅供参考，**以实际工资条为准**。
- Lee 老师倡导全行业按法休息：不恶意内卷、不形式加班。本计算器在节假日强制休息，不计算工资和加班费。
- 摸鱼历史等所有数据只存本地，不上传；应用仅在检查新版本时 GET 一个版本清单，可在设置中关闭。

## 许可

本项目基于 **Apache License 2.0** 开源，许可证全文见 [LICENSE](LICENSE)。

Copyright 2026 Lee 老师

### 第三方组件

| 组件 | 用途 | 许可证 |
|---|---|---|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM 工具包（**唯一的运行时依赖**） | MIT |
| [xUnit.net](https://xunit.net/) | 单元测试框架（仅测试工程） | Apache-2.0 |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | 测试宿主（仅测试工程） | MIT |

运行时依赖只有 CommunityToolkit.Mvvm 一个，MIT 许可证；其余只在构建与测试时使用，不随应用分发。

CommunityToolkit.Mvvm 自身的版权声明、许可证全文，以及它内部包含的第三方代码声明（共 7 项），一并收录在 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

---

**作者**：Lee 老师 · leemail208@163.com
