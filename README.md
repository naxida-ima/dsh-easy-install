# DeepSeek Harness 一键安装包（dsh-easy-install）

让**完全不懂技术的人**也能安装 DeepSeek Harness（DeepSeek 官方开源的 AI 智能体工作台）。
交付物是一个精美的 Windows 便携压缩包，内含 **WinUI 3（Fluent 设计）** 的一键安装向导与桌面开关，解压双击即可安装，全程无需联网、无需懂 GitHub / Node.js / npm。

## 它解决什么问题

官方安装方式 `npx @deepseek-ai/dsh web` 需要：安装 Node.js（且版本 ≥ 22.19）、能访问 npm、会用命令行。
对普通人来说门槛极高。本工具把这一切全部打包：

- ✅ 内置 Node.js 24 LTS（Windows 便携版，免安装、免注册表）
- ✅ 内置 `@deepseek-ai/dsh` 完整依赖树（Windows 原生二进制，离线可用）
- ✅ 环境检测：逐项检查系统位数 / 系统版本 / 内存 / 磁盘 / 端口 / 浏览器 / 网络
- ✅ 精美四步向导：欢迎 → 环境检测 → 安装 → 完成
- ✅ 桌面开关：常驻托盘，绿=运行中 / 灰=已停止，一键启停、开机自启、打开界面、卸载
- ✅ 支持 Windows 10/11 精简版（无需管理员权限，装到 `%LOCALAPPDATA%`）

## 安装位置与数据

| 项目 | 位置 |
|---|---|
| 程序本体 | `%LOCALAPPDATA%\DeepSeekHarness\` |
| Node 运行时 | `%LOCALAPPDATA%\DeepSeekHarness\runtime\node\` |
| dsh 程序 | `%LOCALAPPDATA%\DeepSeekHarness\runtime\app\` |
| 桌面开关 | `%LOCALAPPDATA%\DeepSeekHarness\switch\switch.exe` |
| 服务日志 | `%LOCALAPPDATA%\DeepSeekHarness\logs\` |
| 服务端口 | `http://127.0.0.1:3080` |

## 构建（GitHub Actions，Windows）

推送后自动触发，产物为 `DeepSeek-Harness-Installer-v2.x.zip`（Artifact：`DeepSeek-Harness-Installer`）。

```yaml
# .github/workflows/build.yml 已配置：
# 1. dotnet publish 构建 DshInstaller（安装向导，WinUI 3）与 DshSwitch（桌面开关）
# 2. prepare_bundles.py 下载 Node 便携版 + npm 安装 dsh 离线依赖
# 3. 组装便携包目录并打包 zip，发布 GitHub Release
```

## 本地开发

```bash
pip install customtkinter pystray pillow pywin32
python build/entry_installer.py   # 运行安装向导
python build/entry_switch.py      # 运行桌面开关
python scripts/make_assets.py     # 重新生成图标资源
```

> 注意：`npm install @deepseek-ai/dsh` 必须在 Windows 上执行（依赖含平台原生模块 node-pty / koffi / sharp）。
> `_assets/`（node.zip / dsh.zip / switch.zip / checksums.json / bundle_info.json）由 CI 生成。

## 目录结构

```
dsh-installer/
├── winui/                    # WinUI 3 (C#) 主代码
│   ├── DshInstaller/         # 一键安装向导（5 步流程）
│   │   ├── MainWindow.xaml   # 步骤条 + 页面导航
│   │   ├── Pages/            # 欢迎/检测/可选组件/安装/完成
│   │   └── Core/             # Detector/Installer/Bundle/DshService/OptComponents
│   ├── DshSwitch/            # 桌面开关（大圆开关 + 启停 + 开机自启 + 卸载）
│   └── shared/               # （备用 Python 版本保留在 installer/switch/shared）
├── installer/ switch/ shared/   # Python 版（v1.x 遗留，可回退）
├── scripts/                  # make_assets(图标) / make_zip(打包) / prepare_bundles(离线资源)
└── .github/workflows/build.yml
```

## 免责声明

DeepSeek Harness 为 DeepSeek AI 开源的 MIT 项目（https://github.com/deepseek-ai/deepseek-harness）。
本安装包仅做打包分发，与 DeepSeek 官方无直接关联。使用 AI 功能需自备 DeepSeek API Key。
