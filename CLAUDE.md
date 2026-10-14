# CLAUDE.md

SalaryTicker（实时计算器）—— Windows 桌面工资实时计算器。

本文件是本仓库的**工作规范**。每次会话开始先读它；后续所有改动一律按此执行，不需要用户重复交代。

---

## 一、项目速览

| 项 | 值 |
|---|---|
| 技术栈 | C# / .NET 8 / WPF / MVVM（CommunityToolkit.Mvvm） |
| 主工程 | `SalaryTicker.csproj` —— 版本号在 `<Version>`，发版时只改这里 |
| 测试工程 | `tests/ShenzhenSalaryTimer.Tests/`（xUnit） |
| 用户数据 | `%AppData%\SalaryTicker\`（纯 JSON，无数据库） |
| 官网与下载页 | `server/`（Nginx 配置 + index.html） |
| 线上地址 | https://salaryticker.leelab.com.cn |
| 唯一联网模块 | `Services/UpdateService.cs` / `UpdateLogic.cs` —— 仅"检查新版本"，可在设置中关闭 |

## 二、常用命令

```bash
dotnet build                    # 编译
dotnet test                     # 单元测试
dotnet run                      # 开发运行
dotnet run -- gallery           # 样式画廊（设计系统验收用）
```

**提交前铁律：`dotnet build` 通过且 `dotnet test` 全部通过。** 每一次提交都要保持 `main` 可编译、测试全绿。

## 三、Git 规范

### 分支模型
**只有 `main` 一个分支。** 单人开发，分支只带来合并成本；`main` 始终是最新可发布状态。

需要回退时用 `git revert`（保留历史）或 `git checkout v1.1.2 -- <file>`（取回旧文件），**不要新建分支**。

仓库级身份已固定为 `lhf-hub` / `lhf-hub@users.noreply.github.com`（noreply 地址，不泄露工作邮箱）。

远程仓库 `https://github.com/lhf-hub/salaryticker` **已转为公开**（2026-10-14），以 **Apache License 2.0** 开源，全文见根目录 `LICENSE`。

每次发版会在 GitHub 建一个 **Release**，附上该版本的 exe，作为一份**异地归档**。但**它不参与分发**：应用内「检查更新」始终指向自己的服务器，exe 只从服务器下载 —— GitHub 在国内时通时不通，不适合当分发渠道。

### Commit 规范（Conventional Commits，描述用中文）

```
<type>(<scope>): <简短说明>

<正文：为什么这么改（可选）>
```

**type**：`feat` 新功能 / `fix` 修 bug / `docs` 文档 / `style` 样式与格式 / `refactor` 重构 / `perf` 性能 / `test` 测试 / `build` 构建与依赖 / `chore` 杂项 / `revert` 回滚

**scope**（本项目固定集合，便于检索）：`calc` 计算 / `ui` 界面 / `settings` 设置 / `muyu` 摸鱼 / `mini` 迷你窗 / `update` 更新 / `holiday` 节假日 / `deploy` 部署与脚本 / `docs` 文档 / `tests` 测试

示例：

```
feat(mini): 迷你窗支持切换显示本月/今日/当前小时
fix(holiday): 2026-09-20 补班日名称由「中秋」更正为「国庆」
feat(update): 检查失败时提供「在浏览器中打开官网」兜底入口
```

### Tag 规范
- **只给正式发布打 tag**，格式 `v1.1.3`，必须与 `csproj` 的 `<Version>` **完全一致**；开发过程不打 tag（commit 历史本身就是开发记录）
- 一律用附注 tag：`git tag -a v1.1.3 -m "说明"`
- **tag 不可移动**：发错版本就升版本号，不删旧 tag

## 四、发布流程

```powershell
# 1. 改 SalaryTicker.csproj 的 <Version>，并把本版更新日志写进 tools\release-notes.txt
# 2. 预演（可选）：测试 + 构建 + 本地提交打 tag，不推送
powershell -File tools\release.ps1 -SkipPush
# 3. 测试 + 构建 + 提交 + tag + 推送 + 建 GitHub Release（附 exe）
#    —— 打 tag 前必须 dotnet test 全绿
powershell -File tools\release.ps1
# 4. 把同一个 exe 发给自动更新通道（不重新构建）：
powershell -File tools\deploy.ps1 -SkipPublish
# 5. 官网有改动时：
powershell -File tools\deploy-site.ps1
```

**日常推送**（改文档、重构等不打 tag 的提交）：`powershell -File tools\release.ps1 -NoTag`。不带 `-NoTag` 时脚本会打 tag，而 tag 不可重复 —— csproj 版本号没升就不要用不带开关的调用。

**构建只发生一次，就在第 3 步 `release.ps1`** —— 它内部调 `deploy.ps1 -SkipUpload`，产出 `dist\upload\SalaryTicker-<版本>.exe`，既作为 Release 附件上传，又留给第 4 步复用。`deploy.ps1 -SkipPublish` 直接上传同一个文件、不再重新构建，**保证 GitHub 附件与自动更新下发的是同一份二进制**。

`release.ps1 -NoTag`（日常推送）不会构建、也不会建 Release —— 没有 tag 就没有 Release。

`deploy.ps1` 还会把当前版本合并进 `tools/version-history.json`（官网「历史版本」弹窗的数据源，`tools/version-history.json` 是事实来源、服务器只收一份副本）并上传为站点根目录的 `history.json`。**条目一旦写入就冻结**——已发布的版本号不重用，重复部署同一版本不会改写历史；要改只能手工编辑那个 json。合并后脚本会提示把仓库里的 `version-history.json` 一并提交（它发生在 `release.ps1` 提交之后，所以会随下一次提交进 git）。

### 脚本职责（互不依赖）

| 脚本 | 作用 | 常用开关 |
|---|---|---|
| `tools\release.ps1` | 代码进 GitHub（测试 + 构建 + 提交 + tag + 推送 + Release） | `-NoTag` 日常推送不打 tag / `-SkipPush` 本地预演 / `-SkipBuild` 复用已有构建 / `-Message "<提交信息>"` |
| `tools\deploy.ps1` | 应用进自己的服务器（exe + version.json + history.json + 软链） | `-SkipUpload` 只构建不上传 / `-SkipPublish` 复用 release.ps1 的构建 |
| `tools\deploy-site.ps1` | 官网进自己的服务器（index.html + assets） | `-WithNginx` 同时更新 Nginx 配置 / `-NoVerify` 跳过上传后验证 |

## 五、敏感文件（已 gitignore，**绝不提交**）

- `tools/deploy.config.json` —— 服务器地址、SSH 用户与密钥路径
- `tools/github.config.json` —— GitHub PAT（从 `github.config.sample.json` 复制填写）

提交前自查：`git status --porcelain` 中不应出现上述文件；`git ls-files | grep -E "deploy.config.json|github.config.json"` 应无输出。

**仓库已公开，新增文件前先想一遍会不会泄露服务器信息**：真实的主机名/用户名/密钥路径只许出现在被 gitignore 的配置里，文档与示例一律用占位值（`server/README.md` 曾把真实 SSH 用户名写进示例，2026-10-14 公开前已改掉）。

## 六、编码与文件约定

- **`tools/*.ps1` 必须保持纯 ASCII**：Windows PowerShell 5.1 解码无 BOM 的 .ps1 用系统 ANSI 码页（中文系统为 GBK），非 ASCII 字符会被错误解码、破坏字符串字面量。脚本**读取**的数据文件（如 `release-notes.txt`）仍显式按 UTF-8 处理。
- **写 JSON 用 UTF-8 无 BOM**：PS 5.1 的 `Set-Content -Encoding UTF8` 会加 BOM，可能让客户端 `System.Text.Json` 解析失败，需用 `[System.IO.File]::WriteAllText` + `UTF8Encoding($false)`。
- 本项目 `csproj` 含中文注释，读取时一律 `Get-Content -Encoding UTF8`。
- 无 IL 裁剪：WPF 不支持 `PublishTrimmed`，发布命令中不含该参数。

## 七、产品与文案规范

- **产品名 `SalaryTicker`，展示名「实时计算器」**；不以「深圳专用」自称 —— 内置深圳参数开箱即用，其他城市可在「设置 → 政策参数」自行调整。参数名保留「深圳一档 / 二档」。
- **中文与英文、数字之间加空格**（如「使用 .NET 8 构建」「共 93 项测试」）。
- 摸鱼按钮固定顺序：🚽 去趟 WC → ☕ 倒杯水 → 🚬 抽根烟。
- 幽默文案要**真实**，不编造夸张数据。
- 计算结果一律标注「仅供参考，以实际工资条为准」。

## 八、环境要点与已知坑

- **GitHub 直连被 DPI 按 SNI 拦截**（curl 直连 20 秒无响应），git 必须走本地代理 `http://127.0.0.1:7897`。本仓库的 `.git/config` 已设好 `http.proxy` / `https.proxy`（**不进版本控制**，新克隆需重设）；`tools/github.config.json` 的 `proxy` 字段是脚本侧的来源，`release.ps1` 会据此给 git 显式带上代理。
- **全局 `credential.helper=manager`（Git Credential Manager 2.6.1，已安装）在无人值守时会让 git 挂起**：服务器返回 401 后 GCM 要弹 GUI 登录窗，脚本里没人点就一直等。因此 `release.ps1` 把 token 直接放进 push URL（仅该条命令，不写入 `.git/config`，`git remote -v` 不会泄露）。人工在终端里手动 `git fetch` / `git pull` 时走的是 GCM 正常交互路径 —— 弹一次浏览器授权即可，属预期现象。
- 域名备案已完成（2026-10-10 复测：官网 HTTP 200、`version.json` 与下载链接均正常）。此前被阿里云 ICP 拦截期间官网与「检查更新」会失败，那属于预期现象；现已恢复，若再出现 403 + `Server: Beaver` 即为备案状态异常。
- 应用需放在可写目录（如 `D:\Apps\`）；放在 `Program Files` 下会因权限不足导致自动更新失败。
- 无代码签名证书，首次运行新版本 Windows SmartScreen 会提示「来源不明」，属预期。

## 九、需求文档

`《深圳工资实时计算器》需求方案书.txt` 是最初的需求方案书，随仓库一起版本管理；实现与文档冲突时**以当前代码为准**。
