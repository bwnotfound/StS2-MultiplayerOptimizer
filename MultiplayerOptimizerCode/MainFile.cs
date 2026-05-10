using BaseLib.Config;
using BaseLib.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "MultiplayerOptimizer";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // 注册 Mod 配置
        ModConfigRegistry.Register(ModId, new MultiplayerOptimizerConfig());

        // 注册自定义 act（必须在 PatchAll 之前，因为 ExpandActListPatch 引用 Bootstrap 里的实例）
        ExtraActsBootstrap.Initialize();

        Harmony harmony = new(ModId);
        harmony.PatchAll();
    }
}