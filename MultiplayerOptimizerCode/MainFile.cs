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

    /// <summary>
    /// 当前 mod 版本号。**单一数据源是 MultiplayerOptimizer.json 的 "version" 字段**，
    /// 这里运行时通过 ModManager 反查避免两处版本号需要手动同步。
    ///
    /// 为什么不写成常量：base game 多人加入时（JoinFlow.cs:82）拼 "&lt;mod_id&gt;-&lt;manifest.version&gt;"
    /// 跟 host 对比，如果两个玩家的 manifest 字符串不完全一致就拒绝加入。如果代码里维护一个常量、
    /// json 里维护另一个字符串，两者很容易漂移导致 ModMismatch。
    ///
    /// 修改 mod 版本：**只改 MultiplayerOptimizer.json 的 "version" 字段**，代码无需任何改动。
    ///
    /// 缓存策略：**只缓存有效值**。如果某次返回 "unknown"（不应该，因为已用 ModManager.Mods 而非
    /// GetLoadedMods），下次还会重试——避免一次失败永久 stuck 在 unknown。
    /// </summary>
    public static string ModVersion
    {
        get
        {
            if (_cachedModVersion != null) return _cachedModVersion;
            var v = ResolveModVersion();
            if (v != "unknown") _cachedModVersion = v;
            return v;
        }
    }

    private static string? _cachedModVersion;

    private static string ResolveModVersion()
    {
        try
        {
            // 不用 GetLoadedMods()——它过滤 state == Loaded，但 mod 自己的 Initialize() 期间
            // 自己的 state 可能还没标记为 Loaded（mod loader 顺序：load DLL → 调 Initializer →
            // 标记 Loaded），会导致版本读不到。用 ModManager.Mods（所有 mods）更稳。
            foreach (var mod in ModManager.Mods)
            {
                if (mod.manifest?.id == ModId)
                {
                    return mod.manifest.version ?? "unknown";
                }
            }

            // 找不到自己——理论上不应该发生，manifest 在 mod detection 阶段就被读取了
            Logger.Warn(
                $"[Init] Could not find own manifest in ModManager.Mods. " +
                "ConfigSync version check will report 'unknown'.");
            return "unknown";
        }
        catch (System.Exception ex)
        {
            Logger.Error($"[Init] Failed to resolve mod version from manifest: {ex.Message}");
            return "unknown";
        }
    }

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // 启动时输出版本号到 log，方便排查跨玩家版本不一致问题
        Logger.Info($"[Init] Loading {ModId} version {ModVersion}");

        ModConfigRegistry.Register(ModId, new MultiplayerOptimizerConfig());

        // 必须在 PatchAll 之前——ExpandActListPatch 引用 Bootstrap 里的实例
        ExtraActsBootstrap.Initialize();

        Harmony harmony = new(ModId);
        harmony.PatchAll();
    }
}