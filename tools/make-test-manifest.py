# ============================================================================
# make-test-manifest.py —— 本地更新测试用：生成 version.json
# 用法：python tools/make-test-manifest.py <exe路径> <输出目录> <版本号> [下载URL]
# ============================================================================
import hashlib
import io
import json
import os
import sys
from datetime import datetime, timezone

if len(sys.argv) < 4:
    print("usage: make-test-manifest.py <exe> <outdir> <version> [url]")
    sys.exit(1)

exe_path, out_dir, version = sys.argv[1], sys.argv[2], sys.argv[3]

with open(exe_path, "rb") as f:
    data = f.read()

sha256 = hashlib.sha256(data).hexdigest().upper()

manifest = {
    "version": version,
    "url": sys.argv[4] if len(sys.argv) > 4 else f"releases/SalaryTicker-{version}.exe",
    "sha256": sha256,
    "size": len(data),
    "releaseNotes": "【本地测试】验证一键更新：下载 → SHA256 校验 → 接力替换 → 自动重启",
    "publishedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}

os.makedirs(out_dir, exist_ok=True)
target = os.path.join(out_dir, "version.json")
with io.open(target, "w", encoding="utf-8") as f:
    json.dump(manifest, f, ensure_ascii=False, indent=2)

print(f"manifest -> {target}")
print(f"  version = {version}")
print(f"  size    = {len(data)} bytes")
print(f"  sha256  = {sha256[:24]}...")
