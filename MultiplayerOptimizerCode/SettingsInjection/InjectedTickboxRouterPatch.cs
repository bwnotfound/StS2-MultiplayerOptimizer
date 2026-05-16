using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.SettingsInjection;

// ===================================================================================================
// 设计背景：
//
// 我们的 settings UI 注入策略是「复制 base game 的 UploadGameplayData row 作为模板」——这种做法的
// 副作用：复制出来的新 row 内部仍然是 NUploadDataTickbox 实例，**点击 tickbox 时会触发它的
// OnTick/OnUntick virtual override**，原本会改 PrefsSave.UploadData（base game 的数据上报开关）。
//
// 不能简单 disconnect signal——OnTick/OnUntick 是 C# virtual override，不是通过 Godot signal 绑定。
//
// 解决方案（参考 SteamRandomMatch mod）：patch NUploadDataTickbox 的 OnTick/OnUntick/SetFromSettings
// 三个方法的 prefix，通过 row Name 判断 __instance 是不是在我们注入的 row 里：
//   - 是：改我们 mod 的 config 字段，return false 阻止原方法（不会污染 PrefsSave.UploadData）
//   - 否：return true，让 base game 原 row 的 tickbox 走原逻辑（不影响 UploadGameplayData 功能）
//
// 同时如果用户装了其他也复制 UploadGameplayData row 的 mod（如 SteamRandomMatch），两个 mod
// 各自的 prefix 都会跑，按 row Name 各管各的 row——天然兼容。
// ===================================================================================================

/// <summary>
/// Patch NUploadDataTickbox.OnTick 的 prefix。
/// 如果点击的是我们注入的 row 里的 tickbox，按 row 类型改我们 mod 的 config，return false 阻止
/// 原方法（避免污染 PrefsSave.UploadData）。
/// </summary>
[HarmonyPatch(typeof(NUploadDataTickbox), "OnTick")]
internal static class InjectedTickboxOnTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NUploadDataTickbox __instance)
    {
        var kind = InjectedRowKind.GetKindOf(__instance);
        if (!kind.HasValue) return true; // base game 原 row，走原逻辑

        switch (kind.Value)
        {
            case InjectedRowKind.Kind.EnableExtraSpeed:
                MultiplayerOptimizerConfig.EnableSpeedMultiplier = true;
                MainFile.Logger.Info("EnableSpeedMultiplier toggled ON via settings UI");
                break;
            // ExtraSpeedMultiplier 是 slider，不会触发 tickbox 的 OnTick——但加 case 防御未来扩展
            case InjectedRowKind.Kind.ExtraSpeedMultiplier:
                MainFile.Logger.Warn("Unexpected OnTick on slider row, ignored");
                break;
        }

        return false;
    }
}

/// <summary>
/// Patch NUploadDataTickbox.OnUntick 的 prefix。同 OnTick 镜像逻辑。
/// </summary>
[HarmonyPatch(typeof(NUploadDataTickbox), "OnUntick")]
internal static class InjectedTickboxOnUntickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NUploadDataTickbox __instance)
    {
        var kind = InjectedRowKind.GetKindOf(__instance);
        if (!kind.HasValue) return true;

        switch (kind.Value)
        {
            case InjectedRowKind.Kind.EnableExtraSpeed:
                MultiplayerOptimizerConfig.EnableSpeedMultiplier = false;
                MainFile.Logger.Info("EnableSpeedMultiplier toggled OFF via settings UI");
                break;
            case InjectedRowKind.Kind.ExtraSpeedMultiplier:
                MainFile.Logger.Warn("Unexpected OnUntick on slider row, ignored");
                break;
        }

        return false;
    }
}

/// <summary>
/// Patch NUploadDataTickbox.SetFromSettings 的 prefix。
///
/// 这个方法在两个时机被调：
///   1. _Ready 时（base game 初始化）—— duplicate 出来的 row 也会 _Ready
///   2. 我们 SettingsUiInjectionPatch 主动 RefreshInjectedTickboxes() 时
///
/// 原 base game 逻辑：<c>IsTicked = SaveManager.Instance.PrefsSave.UploadData</c>。
/// 我们要让注入的 row 显示我们 config 的当前值而不是 UploadData——重定向 IsTicked 来源。
/// </summary>
[HarmonyPatch(typeof(NUploadDataTickbox), "SetFromSettings")]
internal static class InjectedTickboxSetFromSettingsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NUploadDataTickbox __instance)
    {
        var kind = InjectedRowKind.GetKindOf(__instance);
        if (!kind.HasValue) return true;

        switch (kind.Value)
        {
            case InjectedRowKind.Kind.EnableExtraSpeed:
                __instance.IsTicked = MultiplayerOptimizerConfig.EnableSpeedMultiplier;
                break;
            case InjectedRowKind.Kind.ExtraSpeedMultiplier:
                // slider row 里也有 NUploadDataTickbox？不会——slider 模板用的是 BgmVolume row，
                // 不含 NUploadDataTickbox。但保险起见 case 写全。
                break;
        }

        return false;
    }
}