using System;
using System.Reflection;
using BaseLib.Config;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 联机配置同步状态管理器。
///
/// 状态机：
///   IsActive=false （默认）：所有 patch 用本地 config，Save/Load 正常
///   ↓ client 收到 ConfigSyncMessage（在 BeginRunLocally 之前）
///   IsActive=true：本地静态字段已被 host 值覆盖，Save 被 WeightNormalizationPatch 拦截
///   ↓ RunManager.CleanUpRun（run 结束）
///   IsActive=false：从磁盘 reload 恢复 client 原配置
///
/// host 自己 IsActive 永远是 false——它用本地 config 即可，且需要正常保存。
/// </summary>
internal static class ConfigSyncManager
{
    /// <summary>
    /// true 表示当前 client 的 MultiplayerOptimizerConfig 字段已被 host 配置覆盖。
    /// WeightNormalizationPatch 用它判断是否拦截 Save（避免把 host 值写到 client 磁盘）。
    /// </summary>
    public static bool IsActive { get; private set; }

    /// <summary>
    /// 当前是否是 host 端。HandleMessage 用它跳过自己 echo back 的消息（如有）。
    /// </summary>
    public static bool IsLocalHost()
    {
        var rm = RunManager.Instance;
        if (rm?.NetService == null) return false;
        return rm.NetService.Type == NetGameType.Host;
    }

    /// <summary>
    /// 把消息里携带的 host 配置 apply 到本地静态字段。
    /// 找不到对应 property 的 key 会被 ignore（mod 版本兼容）。
    /// </summary>
    public static void Apply(ConfigSyncMessage msg)
    {
        var configType = typeof(MultiplayerOptimizerConfig);

        var doublesApplied = 0;
        foreach (var kv in msg.Doubles)
        {
            var prop = configType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(double)) continue;
            try
            {
                prop.SetValue(null, kv.Value);
                doublesApplied++;
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"[Sync] Failed to apply {kv.Key}={kv.Value}: {ex.Message}");
            }
        }

        var boolsApplied = 0;
        foreach (var kv in msg.Bools)
        {
            var prop = configType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(bool)) continue;
            try
            {
                prop.SetValue(null, kv.Value);
                boolsApplied++;
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"[Sync] Failed to apply {kv.Key}={kv.Value}: {ex.Message}");
            }
        }

        IsActive = true;
        MainFile.Logger.Info(
            $"[Sync] Applied host config: {doublesApplied}/{msg.Doubles.Count} doubles, " +
            $"{boolsApplied}/{msg.Bools.Count} bools (skipped keys are unknown to this mod version)");
    }

    /// <summary>
    /// 从磁盘 reload 恢复 client 原配置，并清除 active 标记。
    /// 调用安全：IsActive=false 时是 no-op（host 端 / 单机也调用此方法）。
    /// </summary>
    public static void Restore()
    {
        if (!IsActive) return;

        try
        {
            ModConfig.Load<MultiplayerOptimizerConfig>();
            MainFile.Logger.Info("[Sync] Restored client config from disk");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[Sync] Failed to reload config from disk: {ex.Message}");
        }
        finally
        {
            IsActive = false;
        }
    }
}