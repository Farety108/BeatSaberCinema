using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace BeatSaberCinema.Patches
{
	[HarmonyPatch]
	[UsedImplicitly]
	// ReSharper disable once InconsistentNaming
	internal static class StandardLevelScenesTransitionSetupDataSOInit
	{
		[UsedImplicitly]
		private static MethodInfo TargetMethod() => FindInitMethod(typeof(StandardLevelScenesTransitionSetupDataSO));

		// Older game versions have two Init overloads where one forwards to the other; patch only the one without
		// IBeatmapLevelData to avoid running twice. Since 1.44 there is a single overload that takes IBeatmapLevelData.
		internal static MethodInfo FindInitMethod(Type type)
		{
			var initMethods = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == "Init").ToList();
			return initMethods.FirstOrDefault(m => m.GetParameters().All(p => p.ParameterType != typeof(IBeatmapLevelData)))
			       ?? initMethods.First();
		}

		[UsedImplicitly]
		public static void Prefix(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, ref OverrideEnvironmentSettings overrideEnvironmentSettings)
		{
			//Wrap all of it in try/catch so an exception would not prevent the player from playing songs
			try
			{
				PlaybackController.Instance.SceneTransitionInitCalled();
				VideoMenu.Instance?.SetSelectedLevel(beatmapLevel);

				if (!SettingsStore.Instance.PluginEnabled || SettingsStore.Instance.ForceDisableEnvironmentOverrides)
				{
					Log.Info($"Cinema disabled: {!SettingsStore.Instance.PluginEnabled}, environment override force disabled: {SettingsStore.Instance.ForceDisableEnvironmentOverrides}");
					return;
				}

				var video = PlaybackController.Instance.VideoConfig;
				if (video == null || (!video.IsPlayable && video.forceEnvironmentModifications != true))
				{
					Log.Debug($"No video or not playable, DownloadState: {video?.DownloadState}");
					return;
				}

				// "Force Big Mirror" takes priority over the mapper's environment, the video config's environment
				// and the player's own environment override, so every map with a video uses Big Mirror
				if (!SettingsStore.Instance.OverrideEnvironment)
				{
					if (video.environmentName != null)
					{
						var overrideSettings = GetOverrideEnvironmentSettingsFor(video.environmentName);
						if (overrideSettings != null)
						{
							overrideEnvironmentSettings = overrideSettings;
							Log.Debug($"Overriding environment to {video.environmentName} as configured");
							return;
						}
					}

					if (video.EnvironmentModified)
					{
						Log.Debug("Environment is modified, disabling environment override");
						overrideEnvironmentSettings = null!;
					}

					Log.Debug("Cinema's environment override disallowed by user");
					return;
				}

				var bigMirrorOverrideSettings = GetOverrideEnvironmentSettingsFor("BigMirrorEnvironment");
				if (bigMirrorOverrideSettings == null)
				{
					return;
				}

				overrideEnvironmentSettings = bigMirrorOverrideSettings;
				Log.Info("Overwriting environment to Big Mirror");
			}
			catch (Exception e)
			{
				Log.Warn(e);
			}
		}

		private static OverrideEnvironmentSettings? GetOverrideEnvironmentSettingsFor(string serializedName)
		{
			var environmentInfo = GetEnvironmentInfoFor(serializedName);
			if (environmentInfo == null)
			{
				Log.Error($"Could not find environment environment info for {serializedName}");
				return null;
			}

			var overrideSettings = new OverrideEnvironmentSettings {overrideEnvironments = true};
			overrideSettings.SetEnvironmentInfoForType(environmentInfo.environmentType, environmentInfo);
			return overrideSettings;
		}

		private static EnvironmentInfoSO? GetEnvironmentInfoFor(string serializedName)
		{
			return Resources.FindObjectsOfTypeAll<EnvironmentInfoSO>().FirstOrDefault(x => x.serializedName == serializedName);
		}
	}

	[HarmonyPatch]
	[UsedImplicitly]
	// ReSharper disable once InconsistentNaming
	internal static class MissionLevelScenesTransitionSetupDataSOInit
	{
		[UsedImplicitly]
		private static MethodInfo TargetMethod() => StandardLevelScenesTransitionSetupDataSOInit.FindInitMethod(typeof(MissionLevelScenesTransitionSetupDataSO));

		[UsedImplicitly]
		private static void Prefix(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey)
		{
			try
			{
				var overrideSettings = new OverrideEnvironmentSettings();
				StandardLevelScenesTransitionSetupDataSOInit.Prefix(beatmapLevel, beatmapKey, ref overrideSettings);
			}
			catch (Exception e)
			{
				Log.Warn(e);
			}
		}
	}
}