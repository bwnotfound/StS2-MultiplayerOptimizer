using System;
using System.Linq;
using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// Mod 入口。负责注册配置、引导自定义 act、应用 Harmony patch。
/// </summary>
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "MultiplayerOptimizer";

    // ModVersion 从 manifest 运行时读取，避免代码常量与 manifest 漂移。
    // 只缓存有效值——查不到时不缓存 "unknown"，允许后续重试。
    private static string? _cachedModVersion;

    public static string ModVersion
    {
        get
        {
            if (_cachedModVersion != null) return _cachedModVersion;
            try
            {
                var info = ModManager.Mods?.FirstOrDefault(m => m?.manifest?.id == ModId);
                var v = info?.manifest?.version;
                if (!string.IsNullOrEmpty(v))
                {
                    _cachedModVersion = v;
                    return v;
                }
            }
            catch
            {
                // 不缓存，让下次重试
            }

            return "unknown";
        }
    }

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Logger.Info($"Loading {ModId} {ModVersion}");

        try
        {
            ModConfigRegistry.Register(ModId, new MultiplayerOptimizerConfig());
        }
        catch (Exception ex)
        {
            Logger.Error($"ModConfigRegistry.Register failed: {ex}");
        }

        try
        {
            ExtraActsBootstrap.Initialize();
        }
        catch (Exception ex)
        {
            Logger.Error($"ExtraActsBootstrap.Initialize failed: {ex}");
        }

        // Harmony.PatchAll 整体包 try/catch：单个 patch class 内部异常会被 Harmony 包成
        // HarmonyException 抛出，且**会中断剩余 patch 的应用**。我们宁可某个 patch 没生效，
        // 也不希望整个 mod 因为一个 patch 出错就完全失效。
        try
        {
            var harmony = new Harmony(ModId);
            harmony.PatchAll();
        }
        catch (Exception ex)
        {
            Logger.Error($"Harmony PatchAll failed: {ex}");
        }
    }
}