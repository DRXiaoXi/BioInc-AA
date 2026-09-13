using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using dryginstudios.bioinc.settings;
using dryginstudios.commonscripts.optimization;
using dryginstudios.commonscripts.resourcemanager;

namespace BioIncAA
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	[BepInProcess("BioIncRedemption.exe")]
	public class AAPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "bioinc.mod.antialiasing";
		public const string PluginName = "BioInc AA";
		public const string PluginVersion = "1.3.0";

		public const string Author = "哔哩哔哩: 逐梦之子-晓夕";
		public const string AuthorBilibili = "逐梦之子-晓夕";
		public const string Homepage = "https://space.bilibili.com/";
		public const string RepoHint = "GitHub: BioInc-AA";

		internal const string PrefsMsaa = "BioIncAA.MSAA";
		internal const string PrefsFps = "BioIncAA.FPS";

		// MSAA 档位: 关 / 2x / 4x / 8x
		internal static readonly int[] Levels = { 0, 2, 4, 8 };

		// 帧率档位: 0=默认(游戏锁60), -1=不限, 其余=目标帧率
		internal static readonly int[] FpsLevels = { 0, 90, 120, 144, 165, 240, -1 };
		internal static readonly string[] FpsNames = { "默认 (60)", "90", "120", "144", "165", "240", "不限" };

		internal static ManualLogSource Log;
		internal static ConfigEntry<int> CfgDefaultLevel;
		internal static ConfigEntry<int> CfgFpsUnlock;

		private void Awake()
		{
			Log = Logger;
			CfgDefaultLevel = Config.Bind("MSAA", "DefaultLevel", -1,
				"未手动设置时的默认抗锯齿档位: -1=跟随游戏默认, 0=关, 2/4/8=MSAA倍数");
			CfgFpsUnlock = Config.Bind("Video", "FpsUnlock", 0,
				"未在面板选择时的帧率: 0=保持游戏锁60帧, 其他值=目标帧率, -1=不限");

			new Harmony(PluginGuid).PatchAll(typeof(Patches));
			SceneManager.sceneLoaded += OnSceneLoaded;
			Log.LogInfo($"BioIncAA {PluginVersion} by {Author}");
			Log.LogInfo($"BioIncAA {PluginVersion} loaded. MSAA now = {QualitySettings.antiAliasing}x, targetFPS = {Application.targetFrameRate}");
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			// 每次加载场景后确保档位生效(覆盖游戏自检/质量档切换的写入);
			// 用户未做选择且配置为 -1 时保持游戏默认不动
			int desired = DesiredLevel();
			if (desired >= 0) Apply(desired, force: true);
			ApplyFps(DesiredFps());
		}

		// -1 = 跟随游戏默认
		internal static int DesiredLevel()
		{
			if (PlayerPrefs.HasKey(PrefsMsaa)) return SnapLevel(PlayerPrefs.GetInt(PrefsMsaa, -1));
			return CfgDefaultLevel.Value >= 0 ? SnapLevel(CfgDefaultLevel.Value) : -1;
		}

		internal static int SnapLevel(int samples)
		{
			return Levels.Contains(samples) ? samples : 0;
		}

		internal static int CurrentActual()
		{
			return SnapLevel(QualitySettings.antiAliasing);
		}

		internal static int IndexOfLevel(int samples)
		{
			int i = Array.IndexOf(Levels, samples);
			return i < 0 ? 0 : i;
		}

		internal static void Apply(int samples, bool force = false)
		{
			if (!Levels.Contains(samples)) samples = 0;
			if (!force && QualitySettings.antiAliasing == samples) return;
			QualitySettings.antiAliasing = samples;
			// 重新提交一次分辨率, 促使交换链按新 MSAA 重建
			Screen.SetResolution(Screen.width, Screen.height, Screen.fullScreenMode);
			Log.LogInfo($"MSAA => {samples}x");
		}

		// 0 = 游戏默认(60), -1 = 不限帧
		internal static int DesiredFps()
		{
			if (PlayerPrefs.HasKey(PrefsFps)) return PlayerPrefs.GetInt(PrefsFps, 0);
			return CfgFpsUnlock.Value;
		}

		internal static int IndexOfFps(int fps)
		{
			int i = Array.IndexOf(FpsLevels, fps);
			return i < 0 ? 0 : i;
		}

		internal static void ApplyFps(int fps)
		{
			if (fps == 0)
			{
				Application.targetFrameRate = 60; // 游戏默认 (GameManager.Awake)
				return;
			}
			Application.targetFrameRate = fps;
			// vsync 开启时 Unity 以刷新率为准, 解锁帧率必须关垂直同步
			QualitySettings.vSyncCount = 0;
			Log.LogInfo($"FPS => {(fps < 0 ? "uncapped" : fps.ToString())}");
		}
	}

	[HarmonyPatch]
	internal static class Patches
	{
		// 游戏切换画质档位会 QualitySettings.SetQualityLevel, 从而重置 MSAA —— 打完档位后重应用
		[HarmonyPostfix]
		[HarmonyPatch(typeof(PerformanceManager), nameof(PerformanceManager.ChangeQuality))]
		static void AfterChangeQuality()
		{
			int desired = AAPlugin.DesiredLevel();
			if (desired >= 0) AAPlugin.Apply(desired, force: true);
			AAPlugin.ApplyFps(AAPlugin.DesiredFps());
		}

		// VideoPanel 初始化后向画质面板注入"抗锯齿"与"帧率"两行, 并记录各面板原始尺寸
		[HarmonyPostfix]
		[HarmonyPatch(typeof(VideoPanel), "Awake")]
		static void AfterVideoPanelAwake(VideoPanel __instance) => AAUi.Inject(__instance);

		// 打开面板时把下拉框同步到当前档位
		[HarmonyPostfix]
		[HarmonyPatch(typeof(VideoPanel), nameof(VideoPanel.Open))]
		static void AfterVideoPanelOpen(VideoPanel __instance) => AAUi.Sync(__instance);

		// 修复游戏原生bug: MessageManager 在面板关闭时把"显示期间被拉伸的宽度"写回内容
		// sizeDelta(MessageManager.DestroyMessage), 导致 设置/画质/按键绑定 面板每往返一次就变宽一圈。
		// 每次显示面板时把内容尺寸钉回首次记录的原始值。
		[HarmonyPostfix]
		[HarmonyPatch(typeof(SettingsPanel), nameof(SettingsPanel.ShowSettingsPanel))]
		static void PinMainPanel(SettingsPanel __instance)
		{
			if (__instance.Panel != null) AAUi.Pin(__instance.Panel);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(SettingsPanel), nameof(SettingsPanel.OpenVideoPanel))]
		static void PinVideoPanel(SettingsPanel __instance)
		{
			if (__instance.VideoPanel != null) AAUi.Pin(__instance.VideoPanel.gameObject);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(SettingsPanel), nameof(SettingsPanel.OpenKeybinding))]
		static void PinKeyBindingPanel(SettingsPanel __instance)
		{
			if (__instance.KeyBindingPanel != null) AAUi.Pin(__instance.KeyBindingPanel.gameObject);
		}
	}

	internal static class AAUi
	{
		private const string RowName = "BioIncAA_Row";
		private const string DropdownName = "BioIncAA_Dropdown";
		private const string FpsRowName = "BioIncAA_FpsRow";
		private const string FpsDropdownName = "BioIncAA_FpsDropdown";
		private const string LabelText = "抗锯齿";
		private const string FpsLabelText = "帧率";
		private const string OptionOff = "关闭";

		private class PanelUi
		{
			public TMP_Dropdown Aa;
			public TMP_Dropdown Fps;
		}

		private static readonly Dictionary<VideoPanel, PanelUi> Map = new Dictionary<VideoPanel, PanelUi>();

		// 面板内容的原始尺寸 (在首次 Awake、未被消息系统改动前记录)
		private static readonly Dictionary<RectTransform, Vector2> OriginalSizes = new Dictionary<RectTransform, Vector2>();

		internal static void Inject(VideoPanel panel)
		{
			try
			{
				if (panel == null || Map.ContainsKey(panel)) return;
				CaptureOriginalSizes(panel);
				TMP_Dropdown vsync = panel.verticalSyncDropdown;
				if (vsync == null)
				{
					AAPlugin.Log.LogWarning("verticalSyncDropdown 为空, 无法注入设置行");
					return;
				}

				Transform row = vsync.transform.parent;
				Transform parent = row.parent;

				// ---- 第一行: 抗锯齿 ----
				GameObject aaRow = UnityEngine.Object.Instantiate(row.gameObject, parent);
				aaRow.name = RowName;
				aaRow.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
				OffsetIfNoLayout(parent, row, aaRow.transform, 1);

				TMP_Dropdown ddAa = PrepareDropdown(aaRow, DropdownName);
				if (ddAa == null) return;
				RenameLabel(aaRow, ddAa, LabelText);
				ddAa.ClearOptions();
				ddAa.AddOptions(new List<string> { OptionOff, "MSAA 2x", "MSAA 4x", "MSAA 8x" });
				ddAa.value = AAPlugin.IndexOfLevel(AAPlugin.CurrentActual()); // 在挂监听前赋值, 避免误触发
				ddAa.RefreshShownValue();
				ddAa.onValueChanged.AddListener(delegate (int index)
				{
					int samples = AAPlugin.Levels[Mathf.Clamp(index, 0, AAPlugin.Levels.Length - 1)];
					AAPlugin.Apply(samples);
					PlayerPrefs.SetInt(AAPlugin.PrefsMsaa, samples);
					PlayerPrefs.Save();
				});

				// ---- 第二行: 帧率 (克隆刚做好的抗锯齿行, 已无本地化组件) ----
				GameObject fpsRow = UnityEngine.Object.Instantiate(aaRow, parent);
				fpsRow.name = FpsRowName;
				fpsRow.transform.SetSiblingIndex(aaRow.transform.GetSiblingIndex() + 1);
				OffsetIfNoLayout(parent, aaRow.transform, fpsRow.transform, 1);

				TMP_Dropdown ddFps = PrepareDropdown(fpsRow, FpsDropdownName);
				if (ddFps == null) return;
				RenameLabel(fpsRow, ddFps, FpsLabelText);
				ddFps.ClearOptions();
				ddFps.AddOptions(new List<string>(AAPlugin.FpsNames));
				ddFps.value = AAPlugin.IndexOfFps(AAPlugin.DesiredFps());
				ddFps.RefreshShownValue();
				ddFps.onValueChanged.AddListener(delegate (int index)
				{
					int fps = AAPlugin.FpsLevels[Mathf.Clamp(index, 0, AAPlugin.FpsLevels.Length - 1)];
					AAPlugin.ApplyFps(fps);
					PlayerPrefs.SetInt(AAPlugin.PrefsFps, fps);
					PlayerPrefs.Save();
				});

				Map[panel] = new PanelUi { Aa = ddAa, Fps = ddFps };
				AAPlugin.Log.LogInfo("已在画质面板注入 抗锯齿/帧率 下拉框");
			}
			catch (Exception e)
			{
				AAPlugin.Log.LogError($"注入失败: {e}");
			}
		}

		internal static void Sync(VideoPanel panel)
		{
			if (panel == null || !Map.TryGetValue(panel, out PanelUi ui)) return;
			if (ui.Aa != null)
			{
				ui.Aa.value = AAPlugin.IndexOfLevel(AAPlugin.CurrentActual());
				ui.Aa.RefreshShownValue();
			}
			if (ui.Fps != null)
			{
				ui.Fps.value = AAPlugin.IndexOfFps(AAPlugin.DesiredFps());
				ui.Fps.RefreshShownValue();
			}
		}

		private static TMP_Dropdown PrepareDropdown(GameObject row, string name)
		{
			TMP_Dropdown dd = row.GetComponentInChildren<TMP_Dropdown>(true);
			if (dd == null)
			{
				AAPlugin.Log.LogWarning("克隆行中没有 TMP_Dropdown, 注入中止");
				UnityEngine.Object.Destroy(row);
				return null;
			}
			dd.name = name;
			dd.onValueChanged.RemoveAllListeners();
			return dd;
		}

		// 行标签是 TMP_Text, 但挂着游戏的本地化组件 ResourceUiText,
		// 其 Start() 会把文本刷回本地化文案 —— 先删掉它再改静态文案
		private static void RenameLabel(GameObject row, TMP_Dropdown dd, string text)
		{
			Component[] texts = row.GetComponentsInChildren<Component>(true)
				.Where(t => t is TMP_Text || t is UnityEngine.UI.Text).ToArray();
			bool renamed = false;
			foreach (Component t in texts)
			{
				if (t.transform.IsChildOf(dd.transform)) continue;
				foreach (ResourceUiText loc in t.GetComponents<ResourceUiText>())
					UnityEngine.Object.Destroy(loc);
				if (t is TMP_Text tmp) tmp.text = text;
				else ((UnityEngine.UI.Text)t).text = text;
				renamed = true;
			}
			if (!renamed) AAPlugin.Log.LogWarning($"未找到行标签组件, 标签保持原名 ({text})");
		}

		// 父级没有自动布局时, 手动放到基准行下面第 offset 行
		private static void OffsetIfNoLayout(Transform parent, Transform baseRow, Transform newRow, int rowsBelow)
		{
			if (parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null) return;
			RectTransform baseRt = baseRow as RectTransform;
			RectTransform newRt = newRow as RectTransform;
			if (baseRt == null || newRt == null) return;
			float h = baseRt.rect.height + 4f;
			newRt.anchoredPosition = baseRt.anchoredPosition + new Vector2(0f, -h * rowsBelow);
		}

		private static void CaptureOriginalSizes(VideoPanel panel)
		{
			try
			{
				SettingsPanel sp = panel.GetComponentInParent<SettingsPanel>();
				if (sp == null) return;
				TryCapture(sp.Panel);
				if (sp.KeyBindingPanel != null) TryCapture(sp.KeyBindingPanel.gameObject);
				TryCapture(panel.gameObject);
			}
			catch (Exception e)
			{
				AAPlugin.Log.LogWarning($"记录面板原始尺寸失败: {e.Message}");
			}
		}

		private static void TryCapture(GameObject go)
		{
			if (go == null) return;
			RectTransform rt = go.GetComponent<RectTransform>();
			if (rt != null && !OriginalSizes.ContainsKey(rt)) OriginalSizes[rt] = rt.sizeDelta;
		}

		// 把面板内容尺寸钉回原始值, 抵消 MessageManager 的尺寸回写:
		// 1) DestroyMessage 会把拉伸后的宽度写回 sizeDelta
		// 2) SetContentInMessage 给内容动态挂 LayoutElement, 其 preferred 每轮被更新为拉伸后的宽度
		internal static void Pin(GameObject go)
		{
			if (go == null) return;
			RectTransform rt = go.GetComponent<RectTransform>();
			if (rt == null) return;
			// 惰性记录: 面板第一次显示时必然还是初始尺寸, 以此为基准;
			// (不能依赖 VideoPanel.Awake 才记录 —— 只开关设置不进画质时它永远不会运行)
			if (!OriginalSizes.TryGetValue(rt, out Vector2 size))
			{
				size = rt.sizeDelta;
				OriginalSizes[rt] = size;
			}
			bool changed = false;
			if (rt.sizeDelta != size)
			{
				rt.sizeDelta = size;
				changed = true;
			}
			LayoutElement le = rt.GetComponent<LayoutElement>();
			if (le != null && (le.preferredWidth != (double)size.x || le.preferredHeight != (double)size.y))
			{
				le.preferredWidth = size.x;
				le.preferredHeight = size.y;
				changed = true;
			}
			if (rt.localScale != Vector3.one)
			{
				rt.localScale = Vector3.one;
				changed = true;
			}
			// 窗口内衬(InnerBack)的 LayoutElement 决定消息窗口宽度, 游戏按 内容宽×缩放 计算;
			// 该计算发生在我们复位内容之前, 必须在这里按同一公式重算, 否则窗口仍偏宽
			if (rt.parent is RectTransform parentRt)
			{
				LayoutElement ple = parentRt.GetComponent<LayoutElement>();
				if (ple != null)
				{
					float w = size.x * parentRt.localScale.x;
					float h = size.y * parentRt.localScale.y;
					if (ple.preferredWidth != (double)w || ple.preferredHeight != (double)h)
					{
						ple.preferredWidth = w;
						ple.preferredHeight = h;
						changed = true;
					}
				}
			}
			if (changed) AAPlugin.Log.LogInfo($"已复位面板尺寸 => {size.x:F0}x{size.y:F0}");
		}
	}
}
