# Bio Inc. Redemption — 逆向分析与MOD方案

## 一、逆向结论

| 项目 | 结果 |
|---|---|
| 引擎 | Unity 2022.3.10f1，**Mono** 后端（非 IL2CPP） |
| 游戏代码 | `BioIncRedemption_Data/Managed/Assembly-CSharp.dll`（3MB，**未混淆**，类名/方法名完整保留） |
| 核心玩法数据 | `StreamingAssets/BioInc.db` 与 `BioInc_Symptoms.db` —— **未加密 SQLite** |
| 加载方式 | `GameManager` 启动时调用 `DataLayer.Init()`，以 **读写模式** 直接打开 `StreamingAssets/BioInc.db`（见反编译 `dryginstudios.bioinc.data/DataLayer.cs:30-42`） |
| 存档 | XML，写于 `AppData/LocalLow/DryGin Studios/BioInc Redemption/`（`GameSave.ToXml()`） |
| 服务器 | 每日挑战/排行榜走 DryGin 的 Azure 后端；单机内容完全本地 |

### 数据库结构（主库 BioInc.db）
- `HealthConditionData` — 所有“健康条件”：Type 枚举 `1=Disease(疾病) 2=Cure 3=RiskFactors(风险因素) 4=Recovery 5=StageCondition 6=Diagnosis 7=Treatment 8=Lifestyle 9=IntensiveCare`；`Name` 形如 `Std.Hiv` / `Booster.Smoker`；`Cost`=生物点消耗，`RepeatCount`/`UpdateInterval`=每跳伤害间隔，`Prerequiste`=前置条件表达式（如 `Booster.Smoker()&&Booster.Workaholic()`）
- `AnatomyPointsData` — 每个条件每次发作对 8 大系统（循环/消化/免疫/肌肉/神经/呼吸/骨骼/肾）的伤害值（Init* 为首跳，Repeat* 为后续每跳，负值扣血）
- `TreatmentData` — 每个疾病的治疗方案：`TreatmentTime`(ms)、`Efficiency`(%)、`Cost`
- `CostModifierData` — 条件间的费用增减修正
- `HealthConditionSymptomsData` / `SymptomData` — 症状名与严重度（诊断玩法用，主要在 `BioInc_Symptoms.db`）
- `GameServiceData` — 成就/DLC 服务 ID

### 关键发现
`StreamingAssets/JF modifs/BioInc.db` 是之前有人留下的**手动替换版数据库**（游戏代码中并无 "JF modifs" 引用）。说明社区通行做法就是直接改库文件——本方案与其一致但更安全（带备份）。

## 二、MOD 方案（按推荐程度排序）

### 方案 A：数据库MOD（推荐，零门槛、最稳）
改 `StreamingAssets/BioInc.db`。适合：数值平衡、增删疾病/症状/治疗、新前置链、修改加成器成本等。**重启游戏即生效**。注意 Steam“验证完整性”会还原文件，验证后重新打补丁即可。

工具：`node mod.js`（见下）。

### 方案 B：BepInEx 代码MOD（深度修改）
Unity 2022.3 + Mono → 用 **BepInEx 5.4.23 x64**：
1. 下载 BepInEx_win_x64_5.4.23.zip 解压到游戏根目录（`BioIncRedemption.exe` 旁），运行游戏一次生成配置
2. 用 VS/dotnet 建类库项目，引用 `Managed/` 下的 `Assembly-CSharp.dll` 等，用 HarmonyX 补丁任意方法（如 `Treatment.Init` 改治疗速度、`DataLayer.Init` 让游戏从自定义目录加载MOD数据库——这就能实现“不动原文件的独立MOD”）
3. DLL 放 `BepInEx/plugins/`

适合：改逻辑/UI/解锁、热加载MOD数据库、自定义事件。

### 方案 C：资源MOD
场景/美术/音效在 `globalgamemanagers.assets`、`sharedassets*.assets` 中，用 **AssetStudio**（浏览/导出）+ **UABEA**（替换）改资源；`resources.resource` 里是音频流。

## 三、随附工具使用（方案A）

```bat
node mod.js backup                 :: 首次务必备份
node mod.js list                   :: 列出全部健康条件
node mod.js list Booster           :: 按名筛选
node mod.js info Std.Hiv           :: 查看某条件详情
node mod.js treatments Std.Hiv     :: 查看治疗方案
node mod.js set Std.Hiv Cost=3     :: 改字段（Cost/RepeatCount/UpdateInterval/Prerequiste...）
node mod.js set-treatment 1083 TreatmentTime=5000 Cost=1
node mod.js anatomy 447 nervous=-1 :: 改每跳对各系统伤害(负=伤害, 正=治疗方向)
node mod.js global disease-cost=3  :: 所有费用>3的疾病降到3
node mod.js global treatment-time=8000 clear-prereq
node mod.js patch mods/easy-cure.json  :: 应用JSON补丁包
node mod.js restore                :: 一键还原原版
```

示例补丁包见 `mods/easy-cure.json`。反编译源码在 `decompiled/`（629 个 .cs，直接用 VS 打开 `Assembly-CSharp.csproj` 浏览）。

## 四、注意事项
- 修改前关闭游戏；`backup`/`restore` 只覆盖两个 db 文件
- 想做“疾病MOD包”可整库复制→改→分发，配合方案B的 `DataLayer.Init` 前缀加载即可互不覆盖
- 联机/每日挑战内容在服务器校验，别用于联机作弊
