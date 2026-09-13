# BioInc AA 插件 — 抗锯齿 (MSAA) + 帧率解锁 MOD

> 开发: 哔哩哔哩 **逐梦之子-晓夕** · 内部开发笔记 (对外请看 README_GitHub.md)

为 Bio Inc. Redemption 的 **设置 → 画质** 面板注入两行设置：

- **抗锯齿**：关闭 / MSAA 2x / MSAA 4x / MSAA 8x
- **帧率**：默认 (60) / 90 / 120 / 144 / 165 / 240 / 不限

选择即时生效、自动记住；切画质档位后自动重应用。

> 有趣的发现：游戏最高画质档本身就预置了 MSAA 8x（插件启动日志可见 `MSAA now = 8x`），但官方面板里没有任何调节入口。PC 版帧率被 `GameManager.Awake` 锁在 60 帧。本插件把两者都开放出来，未做选择时完全保持游戏默认行为。

## 已实现 & 已验证

| 功能 | 状态 |
|---|---|
| 画质面板注入"抗锯齿"下拉框（关闭/2x/4x/8x） | ✅ 实测截图确认 |
| 画质面板注入"帧率"下拉框（默认60/90/120/144/165/240/不限） | ✅ 实测截图确认 |
| MSAA 修改立即生效（`QualitySettings.antiAliasing` + 交换链重建） | ✅ 日志 `MSAA => 0x / 8x` |
| 帧率修改立即生效（`Application.targetFrameRate`，解锁档自动关垂直同步） | ✅ 日志 `FPS => 144` |
| 切换画质档位（最快↔出色）后 MSAA 与帧率自动重新应用 | ✅ 已打补丁 `PerformanceManager.ChangeQuality` |
| 两项选择均通过 PlayerPrefs 持久化（注册表 `HKCU\Software\DryGin Studios\BioIncRedemption`） | ✅ 实测 `BioIncAA.MSAA=8`、`BioIncAA.FPS=144` |
| 未选择档位时跟随游戏默认（配置项 -1 / 0） | ✅ 实测 |
| 修复游戏原生bug：反复开关 设置/画质/按键绑定 面板越开越宽 | ✅ 实测：3次画质往返所有尺寸数值恒定 |
| 附加：`FpsUnlock` 配置（游戏锁 60 帧，见 `GameManager.Awake`） | 可选，作为面板未选择时的默认值 |

### 面板越开越宽的修复（v1.2.0）

原生 bug：`MessageManager` 在面板关闭时把"显示期间被拉伸的宽度"写回内容（`DestroyMessage` 中 `sizeDelta = LayoutElement.preferred`），且 `SetContentInMessage` 按展示时宽度重算窗口内衬 `InnerBack.LayoutElement.preferred`，两者形成正反馈——每次开合面板（尤其画质往返后）整个面板变宽一圈，最终撑满屏幕。

修复：每次显示 设置/画质/按键绑定 面板时（Harmony postfix `ShowSettingsPanel` / `OpenVideoPanel` / `OpenKeybinding`），把内容 `sizeDelta`、动态 `LayoutElement.preferred`、`localScale` 钉回首次记录的原始值，并按游戏公式 `原始尺寸 × 内衬缩放(1.25)` 重算窗口内衬的 preferred。实测 3 次画质往返，所有尺寸数值恒定（内容 400x579 / 内衬 500x724 / 画质 300x413）。

## 安装（已装好，换机时照做）

1. 解压 `BepInEx_win_x64_5.4.23.2.zip` 到游戏根目录（`BioIncRedemption.exe` 旁），运行游戏一次
2. 把 `BioIncAA/bin/Release/BioIncAA.dll` 放进 `BepInEx/plugins/`

卸载：删除 `BepInEx/plugins/BioIncAA.dll`（`winhttp.dll` 等其他 BepInEx 文件可一并删）。

## 配置

`BepInEx/config/bioinc.mod.antialiasing.cfg`：

```ini
[MSAA]
# 未手动设置时的默认抗锯齿档位: -1=跟随游戏默认, 0=关, 2/4/8=MSAA倍数
DefaultLevel = -1

[Video]
# 面板未选择时的帧率: 0=保持游戏锁60帧, 其他值=目标帧率, -1=不限
FpsUnlock = 0
```

面板里的选择保存在 PlayerPrefs，优先级高于本配置。

## 帧率档位说明

- **默认 (60)**：不动游戏设定（PC 版锁 60 帧）
- **90~240**：`Application.targetFrameRate = 对应值`，并自动关闭垂直同步（vsync 开启时 Unity 以显示器刷新率为准，会压制帧率上限）
- **不限**：`targetFrameRate = -1`，按硬件性能全速渲染
- 与"垂直同步"互斥：选了解锁档位后又在游戏里开垂直同步的话，实际帧率以刷新率为准
- 游戏逻辑为计时器驱动（毫秒间隔），帧率不影响模拟速度；高帧主要带来流畅度与输入延迟收益

## 构建

```
cd BioIncAA
dotnet build -c Release
```

引用游戏目录的 `Assembly-CSharp.dll`、`UnityEngine*.dll`、`Unity.TextMeshPro.dll` 与 `BepInEx/core/` 下的 `BepInEx.dll`、`0Harmony.dll`（csproj 内 HintPath 指向本机游戏路径）。

## 实现要点（踩过的坑）

1. **UI 注入**：Harmony Postfix `VideoPanel.Awake`，克隆"垂直同步"整行控件 → 改名 → `SetSiblingIndex` 插到垂直同步行下方；面板父级有 VerticalLayoutGroup，自动排版
2. **本地化覆盖**：行标签挂着游戏的 `ResourceUiText` 组件，其 `Start()` 会把文本刷回本地化文案（注入瞬间甚至是法语 `SYNCRONISATION VERTICALE`），必须先 `Destroy` 该组件再写"抗锯齿"
3. **档位被重置**：游戏切画质档调用 `QualitySettings.SetQualityLevel` 会重置 MSAA，Postfix `PerformanceManager.ChangeQuality` 重新应用
4. **赋值时机**：下拉框 `value` 必须在挂 `onValueChanged` 监听之前赋值，避免打开面板时误触发回调
5. 改动分辨率/窗口模式时 Unity 会重建交换链，`QualitySettings.antiAliasing` 是全局属性，不受影响；插件在 `Screen.SetResolution` 后重新提交一次分辨率确保 MSAA 立即生效

## 已知限制

- 手柄导航没有把新下拉框编入方向键链（鼠标操作不受影响）
- MSAA 只影响 3D 渲染边缘，UI 文字由 TMP SDF 渲染本身无锯齿
- 若 Steam"验证完整性"还原了 `winhttp.dll`/`doorstop_config.ini`，重装 BepInEx 即可（插件 DLL 不在校验范围）
