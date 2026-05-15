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
///
/// ## ⚠️ 关键安全设计
///
/// ModConfig.Save 是 BaseLib 提供的、被<b>所有 mod 共用</b>的入口。我们 patch 这里 prefix，
/// 任何 mod 的 ModConfig.Save() 调用都会先走我们的 prefix。所以：
///
///   1. <b>必须先判断 __instance is MultiplayerOptimizerConfig</b>。不是我们的 config 直接
///      return true 放行——绝对不能对其他 mod 的 config 做任何操作。
///
///   2. <b>所有逻辑必须包在 try/catch 里</b>。我们的 prefix 抛异常会让原 Save 方法被跳过，
///      其他 mod 调用 Save 时就什么也存不下，严重影响整个游戏 mod 生态。异常时一定要
///      return true 让原方法跑（即使我们的归一化失败，原始数据先存上比什么都不存好）。
///
///   3. <b>每组 NormalizeTriple 独立 try</b>。一组失败不影响其他四组归一化。
///
/// ## 关于 PatchScope.IsEnabled
/// 这里<b>不</b> honor Enabled。即使总开关关了，写到磁盘的权重也应该归一化——以便用户重新
/// 打开开关时配置仍然合理。但是 ConfigSyncManager.IsActive 的 sync 拦截必须始终生效，否则
/// 会覆盖 client 磁盘。
/// </summary>
[HarmonyPatch(typeof(ModConfig), nameof(ModConfig.Save))]
public static class WeightNormalizationPatch
{
    [HarmonyPriority(Priority.Low)]
    [HarmonyPrefix]
    public static bool BeforeSave(ModConfig __instance)
    {
        // 大 try/catch：任何异常都 return true 让原 Save 跑
        try
        {
            // ⚠️ 必须最先：不是我们的 config，立即放行不做任何操作
            if (__instance is not MultiplayerOptimizerConfig) return true;

            // 联机 client：sync 期间不让保存
            if (ConfigSyncManager.IsActive)
            {
                MainFile.Logger.Info("Save() suppressed during host config sync");
                return false; // 跳过原 Save 方法
            }

            // 正常路径：归一化 5 组权重，每组独立 try 防止一组失败影响其他
            SafeNormalize("Act4Enc", Act4EncProps);
            SafeNormalize("Act4Event", Act4EventProps);
            SafeNormalize("Act4Boss", Act4BossProps);
            SafeNormalize("Act5Event", Act5EventProps);
            SafeNormalize("Act5Boss", Act5BossProps);

            return true; // 让 Save 继续跑
        }
        catch (Exception ex)
        {
            // 最后一道防线——绝对不能让其他 mod 的 Save 因为我们的 prefix 失败
            try
            {
                MainFile.Logger.Error($"WeightNormalizationPatch.BeforeSave outer failure: {ex}");
            }
            catch
            {
                // logger 都挂了
            }

            return true; // 安全放行
        }
    }

    private static void SafeNormalize(string label, PropertyInfo[]? props)
    {
        if (props == null) return; // LoadProps 失败时返回 null
        try
        {
            NormalizeTriple(label, props);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Normalize {label} failed (skipped): {ex}");
        }
    }

    private static void NormalizeTriple(string label, PropertyInfo[] props)
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
                $"Weight triple [{label}] sum=0, restored to defaults");
            return;
        }

        if (Math.Abs(sum - 1.0) < 1e-6) return;

        props[0].SetValue(null, v1 / sum);
        props[1].SetValue(null, v2 / sum);
        props[2].SetValue(null, v3 / sum);
        MainFile.Logger.Info(
            $"Normalized weight triple [{label}]: " +
            $"({v1:F3}, {v2:F3}, {v3:F3}) sum={sum:F3} -> ({v1 / sum:F3}, {v2 / sum:F3}, {v3 / sum:F3})");
    }

    // ---------- PropertyInfo lookups（一次性反射，缓存） ----------
    //
    // LoadProps 失败时返回 null（property rename 等极端情况），SafeNormalize 会跳过。
    // 之前版本 throw 会让整个静态构造函数失败，PatchAll 时 TypeInitializationException
    // 让本 patch 完全不绑——更糟糕。

    private static readonly PropertyInfo[]? Act4EncProps = LoadPropsOrNull(
        nameof(MultiplayerOptimizerConfig.Act4_EncWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act4_EncWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act4_EncWeight_Act3));

    private static readonly PropertyInfo[]? Act4EventProps = LoadPropsOrNull(
        nameof(MultiplayerOptimizerConfig.Act4_EventWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act4_EventWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act4_EventWeight_Act3));

    private static readonly PropertyInfo[]? Act4BossProps = LoadPropsOrNull(
        nameof(MultiplayerOptimizerConfig.Act4_BossWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act4_BossWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act4_BossWeight_Act3));

    private static readonly PropertyInfo[]? Act5EventProps = LoadPropsOrNull(
        nameof(MultiplayerOptimizerConfig.Act5_EventWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act5_EventWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act5_EventWeight_Act3));

    private static readonly PropertyInfo[]? Act5BossProps = LoadPropsOrNull(
        nameof(MultiplayerOptimizerConfig.Act5_BossWeight_Act1),
        nameof(MultiplayerOptimizerConfig.Act5_BossWeight_Act2),
        nameof(MultiplayerOptimizerConfig.Act5_BossWeight_Act3));

    private static PropertyInfo[]? LoadPropsOrNull(params string[] names)
    {
        try
        {
            var arr = names
                .Select(n => typeof(MultiplayerOptimizerConfig).GetProperty(n))
                .ToArray();
            if (arr.Any(p => p == null)) return null;
            return arr!;
        }
        catch
        {
            return null;
        }
    }
}