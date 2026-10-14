# SalaryTicker 服务端部署（Ubuntu）

托管下载页与版本清单，供用户网页下载、应用内一键更新。

## 一、目录结构

服务器上最终长这样：

```
/var/www/salaryticker/
├── index.html                    官网（单页：介绍 + 下载 + FAQ + 更新日志）
├── assets/
│   ├── app-icon.png              应用图标（导航 Logo 与社交分享图）
│   └── favicon.ico               浏览器标签图标
├── version.json                  版本清单（官网展示 + 应用判断更新）
└── download/
    ├── SalaryTicker.exe -> releases/SalaryTicker-1.1.0.exe   简洁下载路径
    └── releases/
        ├── SalaryTicker-1.0.0.exe    带版本号的归档
        └── SalaryTicker-1.1.0.exe
```

对应网址：

| 用途 | URL |
|---|---|
| 官网 | `https://salaryticker.leelab.com.cn/` |
| **下载（简洁路径）** | `https://salaryticker.leelab.com.cn/download/SalaryTicker.exe` |
| 指定版本 | `https://salaryticker.leelab.com.cn/download/releases/SalaryTicker-1.1.0.exe` |
| 版本清单 | `https://salaryticker.leelab.com.cn/version.json` |

> `version.json` 与 `download/` 下的文件全部由 `tools/deploy.ps1` 在发布时自动生成上传，**不要手动放**。
> `download/SalaryTicker.exe` 是指向最新归档的软链，所以下载路径永远简洁、永远是最新版。

## 二、服务器准备

```bash
# 1. 安装 Nginx
sudo apt update && sudo apt install -y nginx

# 2. 建目录（$USER 换成你的登录用户，通常是 ubuntu）
sudo mkdir -p /var/www/salaryticker/releases
sudo chown -R $USER:www-data /var/www/salaryticker
sudo chmod -R 755 /var/www/salaryticker

# 3. 上传官网（index.html 与 assets/ 都在本目录下）
scp index.html ubuntu@<服务器>:/var/www/salaryticker/
scp -r assets   ubuntu@<服务器>:/var/www/salaryticker/

# 4. 配置站点（注意文件名带 .conf）
sudo cp nginx-salaryticker.conf /etc/nginx/sites-available/salaryticker.conf
sudo sed -i 's/your-domain.com/<你的域名>/' /etc/nginx/sites-available/salaryticker.conf
sudo ln -sf /etc/nginx/sites-available/salaryticker.conf /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default      # 可选：去掉默认站点
sudo nginx -t && sudo systemctl reload nginx

# 以后修改配置无需手动操作，用：powershell -File tools\deploy-site.ps1 -WithNginx
```

### 若域名当前指向另一个应用（顶替场景）

```bash
# 查看现有站点配置，记下它的 root 与 server_name
ls /etc/nginx/sites-enabled/
grep -rE "server_name|root" /etc/nginx/sites-enabled/

# 方式 A：直接顶替（原应用下线）
#   把原站点的 server_name 改为别的域名或注释掉，启用 salaryticker 站点：
sudo rm -f /etc/nginx/sites-enabled/<原站点>
sudo ln -sf /etc/nginx/sites-available/salaryticker /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# 方式 B：两个应用共存（官网在 /，原应用挪到子路径）
#   在原应用的 server 块里加 location 前缀，或给原应用单独分配子域名
```

## 三、HTTPS（域名解析生效后）

### 方案 A：certbot（配合 Nginx，最常用）

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d your-domain.com
# 会自动写入证书路径并加上 HTTP→HTTPS 跳转
sudo systemctl status certbot.timer    # 自动续期已在 apt 安装时配置好
```

### 方案 B：Caddy（更省心，自动签发与续期）

Caddy 只需一个 Caddyfile：

```
your-domain.com {
    root * /var/www/salaryticker
    file_server
    header /version.json Cache-Control "no-store"
    header /releases/* Accept-Ranges bytes
}
```

然后 `sudo systemctl reload caddy`，证书与跳转全自动。

## 四、发布新版本

应用与官网是**两个独立的一键脚本**（节奏不同，互不影响）。

### 发应用（exe + 版本清单）

```powershell
notepad tools\release-notes.txt          # 1. 写本版更新日志
notepad SalaryTicker.csproj              # 2. 改 <Version>
powershell -File tools\deploy.ps1        # 3. 构建 + 哈希 + 清单 + 上传 + 更新软链
```

### 发官网（index.html + assets/）

```powershell
powershell -File tools\deploy-site.ps1              # 只发官网
powershell -File tools\deploy-site.ps1 -WithNginx   # 顺带更新 nginx 配置
```

`-WithNginx` 会自动完成：上传配置到暂存路径 → 备份为 `/etc/nginx/sites-available/salaryticker.conf.bak` → 覆盖 → `sudo nginx -t` 校验 → **通过才 reload，不通过自动回滚**（站点不会因此挂掉）。

> **备份文件放在哪儿安全吗？** 安全。nginx 只 `include /etc/nginx/sites-enabled/*`，`sites-available/` 下的文件不会被加载 —— 所以 `salaryticker.conf.bak` 放它旁边既显眼又无害。
>
> **sudo 会问几次密码？** 一次。sudo 有时间戳缓存（Ubuntu 默认 15 分钟），脚本里第一次输完密码，后面的备份/校验/reload 都免密。
>
> 静态文件（html/图片/json）上传后立即生效，**不需要 reload nginx**。

### 首次使用前的准备

复制 `tools/deploy.config.sample.json` 为 `tools/deploy.config.json` 并填入服务器信息（该文件已在 .gitignore 中，不会被提交）：

```json
{
  "host": "your-server.example.com",
  "user": "your-ssh-user",
  "port": 22,
  "keyPath": "C:\\Users\\You\\.ssh\\id_rsa",
  "remoteDir": "/var/www/salaryticker",
  "publicUrl": "https://your-server.example.com"
}
```

只想本地构建不传服务器：`tools\deploy.ps1 -SkipUpload`，产物在 `dist\upload\`。

## 五、验证

```bash
# 清单不可缓存（必须看到 no-store）
curl -sI https://<域名>/version.json | grep -i cache-control

# 安装包支持断点续传 + 正确的 MIME
curl -sI https://<域名>/releases/SalaryTicker-1.1.0.exe | grep -iE "accept-ranges|content-type|content-length"

# 下载页可访问
curl -s https://<域名>/ | head -5
```

应用侧：把 `%AppData%\SalaryTicker\settings.json` 里的 `UpdateCheckUrl` 设为
`https://<域名>/version.json`，或在应用内 **设置 → 更新** 中填写，然后点「立即检查更新」。

## 六、常见问题

| 现象 | 原因 / 处理 |
|---|---|
| 应用提示"无法连接更新服务器" | 检查 `UpdateCheckUrl` 是否可访问；HTTP 站点确认没有强制跳转；防火墙放行 80/443 |
| 用户点更新后无反应 | 应用所在目录不可写（如 `Program Files`）—— 提示用户移到 `D:\Apps\` 之类可写目录 |
| 每次都提示同一版本 | `version.json` 被缓存了，确认 Nginx 的 `Cache-Control: no-store` 生效 |
| 更新后仍显示旧版本号 | 让用户手动重启一次；或检查接力脚本是否被杀软拦截 |
| 下载速度慢 | 69MB 单文件，可考虑在 Nginx 开启 `gzip_static`（对 exe 无效）或使用 CDN 回源 |
