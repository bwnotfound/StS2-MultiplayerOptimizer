using System;
using System.Linq;
using System.Reflection;
using BaseLib.Config;
using HarmonyLib;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 在 ModConfig.Save() 之前做两件事：
///
/// 1. 联机 sync 期间禁止保存：client 端 ConfigSyncManager.IsActive=true 时，
///    本地 MultiplayerOptimizerConfig 字段已经被 host 配置覆盖，
///    如果允许保存会把 host 的值写到 client 磁盘 → 永久丢失 client 原配置。
///    这种情况下直接拦截 Save，return false 跳过整个原方法。
///
/// 2. 单机 / host 端正常路径：对 5 组池权重做归一化（sum 归到 1）。
///    sum&lt;=0 时恢复 ExtraActsConfig.DefaultWeights 避免除 0。
///    这是写入磁盘前的归一化；运行时业务代码也通过 ExtraActsConfig.Get*Weights 二次归一，
///    即使绕过保存（手改 ini）也能保证业务逻辑拿到正常权重。
/// </summary>
[HarmonyPatch(typeof(ModConfig), nameof(ModConfig.Save))]
public static class WeightNormalizationPatch
{
    [HarmonyPrefix]
    public static bool BeforeSave(ModConfig __instance)
    {
        if (__instance is not MultiplayerOptimizerConfig) return true;

        // 联机 client：sync 期间不让保存
        if (ConfigSyncManager.IsActive)
        {
            MainFile.Logger.Info("[Sync] Save() suppressed during host config sync");
            return false; // 跳过原 Save 方法
        }

        // 正常路径：归一化 5 组权重
        NormalizeTriple(Act4EncProps);
        NormalizeTriple(Act4EventProps);
        NormalizeTriple(Act4BossProps);
        NormalizeTriple(Act5EventProps);
        NormalizeTriple(Act5BossProps);

        return true; // 让 Save 继续跑
    }

    private static void NormalizeTriple(PropertyInfo[] props)
    {
        var v1 = (double)props[0].GetValue(null)!;
        var v2 = (double)props[1].GetValue(null)!;
        var v3 = (double)props[2].GetValue(null)!;
        var sum = v1 + v2 + v3;

        if (sum <= 1e-9)
        {
            var d = ExtraActsConfig.DefaultWeights;
            props[0].SetValue(null, d.Act1);
            props[1].SetValue(null, d.Act2);
            props[2].SetValue(null, d.Act3);
            MainFile.Logger.Info(
                $"[ExtraActs] Weight triple [{props[0].Name}] sum=0, restored to defaults");
            return;
        }

        if (Math.Abs(sum - 1.0) < 1e-6) return;

        props[0].SetValue(null, v1 / sum);
        props[1].SetValue(null, v2 / sum);
        props[2].SetValue(null, v3 / sum);
        MainFile.Logger.Info(
            $"[ExtraActs] Normalized weight triple [{props[0].Name}]: " +
            $"({v1:F3}, {v2:F3}, {v3:F3}) sum={sum:F3} -> ({v1 / sum:F3}, {v2 / sum:F3}, {v3 / sum:F3})");
    }

    // ---------- PropertyInfo lookups（一次性反射，缓存） ----------

    private static readonly PropertyInfo[] Act4EncProps = LoadProps(
        nameof(MultiplayerOptimizerConfig.Act4_EncWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act4_EncWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act4_EncWeight_Act3));

    private static readonly PropertyInfo[] Act4EventProps = LoadProps(
        nameof(MultiplayerOptimizerConfig.Act4_EventWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act4_EventWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act4_EventWeight_Act3));

    private static readonly PropertyInfo[] Act4BossProps = LoadProps(
        nameof(MultiplayerOptimizerConfig.Act4_BossWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act4_BossWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act4_BossWeight_Act3));

    private static readonly PropertyInfo[] Act5EventProps = LoadProps(
        nameof(MultiplayerOptimizerConfig.Act5_EventWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act5_EventWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act5_EventWeight_Act3));

    private static readonly PropertyInfo[] Act5BossProps = LoadProps(
        nameof(MultiplayerOptimizerConfig.Act5_BossWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act5_BossWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act5_BossWeight_Act3));

    private static PropertyInfo[] LoadProps(params string[] names)
    {
        return names.Select(n => typeof(MultiplayerOptimizerConfig).GetProperty(n)
                                 ?? throw new InvalidOperationException(
                                     $"WeightNormalizationPatch: property {n} not found on MultiplayerOptimizerConfig")
        ).ToArray();
    }
}