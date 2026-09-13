# BioInc AA

[中文](#功能) | [English](#english)

**Quality-of-life graphics MOD for [Bio Inc. Redemption](https://store.steampowered.com/app/612470/)** — adds an **Anti-Aliasing (MSAA)** selector and **FPS unlock tiers** to the in-game video settings panel, and fixes a vanilla bug where the settings panel grows wider every time you open it.

> 开发 / Developed by **哔哩哔哩：逐梦之子-晓夕** · [Bilibili 主页](https://space.bilibili.com/)

![platform](https://img.shields.io/badge/platform-Windows-blue) ![engine](https://img.shields.io/badge/Unity-2022.3.10f1%20Mono-orange) ![bepinex](https://img.shields.io/badge/BepInEx-5.4.23.2-green)

---

## 功能

在游戏内置 **设置 → 画质** 面板注入两行设置（选择立即生效、自动保存、切换画质档后自动重应用）：

| 新增设置 | 选项 |
|---|---|
| **抗锯齿** | 关闭 / MSAA 2x / MSAA 4x / MSAA 8x |
| **帧率** | 默认 (60) / 90 / 120 / 144 / 165 / 240 / 不限 |

另外修复了一个原版 bug：`MessageManager` 在面板关闭时把拉伸后的宽度写回内容、再按拉伸值重算窗口内衬，形成正反馈——**设置/画质/按键绑定面板每开合一次就变宽一圈**。本 MOD 在每次面板显示时把尺寸钉回原始值。

有趣的事实：游戏最高画质档本身就预置了 MSAA 8x，但官方面板没有给入口；PC 版帧率被硬锁 60。这个 MOD 把两者都开放出来。

## 安装

### 一键安装包（推荐）

1. 从 [Releases](../../releases) 下载 `BioIncModSetup.zip` 并解压
2. 双击 **安装MOD.bat**
3. 启动游戏 → 设置 → 画质

卸载：双击 **卸载MOD.bat**。只删除 MOD 自己的文件，原版游戏文件零接触。

### 手动安装

1. 安装 [BepInEx 5.4.23.2 (x64)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2)：把压缩包内容解压到游戏根目录（`BioIncRedemption.exe` 旁），运行游戏一次
2. 从 [Releases](../../releases) 下载 `BioIncAA.dll`，放进 `BepInEx/plugins/`

## 配置

`BepInEx/config/bioinc.mod.antialiasing.cfg`：

```ini
[MSAA]
# 面板未选择时的默认抗锯齿档位: -1=跟随游戏默认, 0=关, 2/4/8=MSAA倍数
DefaultLevel = -1

[Video]
# 面板未选择时的帧率: 0=保持游戏锁60帧, -1=不限, 其他=目标帧率
FpsUnlock = 0
```

面板里的选择保存在 PlayerPrefs（注册表 `HKCU\Software\DryGin Studios\BioIncRedemption`），优先级高于配置文件。

## 从源码构建

依赖：.NET SDK（任意近期版本，目标框架 net472）。

```bat
git clone https://github.com/<you>/BioInc-AA.git
cd BioInc-AA/BioIncAA
dotnet build -c Release
```

csproj 通过 HintPath 引用本机游戏目录的程序集（默认 `C:\Program Files (x86)\Steam\steamapps\common\Bio Inc. Redemption\`），路径不同请改 csproj。CI 使用在线 NuGet 包解析游戏程序集，无需本机安装游戏（见 `.github/workflows/build.yml`）。

## 项目结构

```
BioIncAA/            插件源码 (C#, HarmonyX)
setup/               一键安装/卸载脚本 + 打包 payload
mod.js               数据库数值MOD工具 (Node.js, 改 StreamingAssets/BioInc.db)
README_GitHub.md     本文件
```

## 已知限制

- 手柄导航未把新增下拉框编入方向键链（鼠标操作不受影响）
- MSAA 只作用于 3D 画面；UI 文字由 TMP SDF 渲染，本身无锯齿
- 帧率解锁档位会自动关闭垂直同步（vsync 开启时 Unity 以显示器刷新率为准）
- 联机/每日挑战数据在服务器侧，本 MOD 不涉及也不影响

## 许可

[MIT](BioIncAA/LICENSE) © 2026 逐梦之子-晓夕 (Bilibili)

本 MOD 以运行时注入（Harmony）方式工作，不分发任何游戏原版文件。Bio Inc. Redemption 是 DryGin Studios 的商标，本项目中与其无隶属关系。

---

<a name="english"></a>
## English

A BepInEx plugin for **Bio Inc. Redemption** that injects two new rows into the game's built-in **Settings → Video** panel:

| Setting | Options |
|---|---|
| **Anti-Aliasing** | Off / MSAA 2x / MSAA 4x / MSAA 8x |
| **Frame Rate** | Default (60) / 90 / 120 / 144 / 165 / 240 / Unlimited |

Changes apply instantly, persist across restarts, and are re-applied whenever the game switches quality presets. It also fixes a vanilla bug where the settings panel grows wider on every open (a positive feedback loop in the game's `MessageManager`).

Fun fact: the game's top quality preset already ships MSAA 8x — the panel just never exposed it — and the PC build is hard-locked to 60 FPS. This mod unlocks both.

**Install**: grab `BioIncModSetup.zip` from [Releases](../../releases), unzip, run `安装MOD.bat` (or install BepInEx 5.4.23.2 x64 manually and drop `BioIncAA.dll` into `BepInEx/plugins/`).

**License**: MIT — see [LICENSE](BioIncAA/LICENSE). Developed by **逐梦之子-晓夕** on Bilibili.
