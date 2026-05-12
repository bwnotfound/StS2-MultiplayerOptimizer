using System;
using System.Linq;
using System.Reflection;
using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "MultiplayerOptimizer";

    // ModVersion 运行时从 manifest 读，避免代码 const 跟 manifest 漂移。
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
        Logger.Info($"[Init] Loading {ModId} version {ModVersion}");

        ModConfigRegistry.Register(ModId, new MultiplayerOptimizerConfig());
        ExtraActsBootstrap.Initialize();

        Harmony harmony = new(ModId);

        // ============================================================
        // PatchAll + 诊断
        // 临时附加：暴露 Harmony 是否 silent-miss 某些 patch（如 lobby ctor）
        // 朋友机器上 EarlyRegister postfix 没跑，需要 log 来锁定具体原因。
        // 排查完毕后这一整段可以删掉。
        // ============================================================
        try
        {
            harmony.PatchAll();
            Logger.Info("[Diagnostic] Harmony PatchAll completed without throwing");
        }
        catch (Exception ex)
        {
            Logger.Error($"[Diagnostic] Harmony PatchAll THREW: {ex}");
        }

        // 诊断 1: 列出 Harmony 实际绑定到的方法。
        // 如果 StartRunLobby/LoadRunLobby 的 ctor 不在列表 = patch 没绑上（silent miss）
        try
        {
            var patched = harmony.GetPatchedMethods().ToList();
            Logger.Info($"[Diagnostic] Harmony bound {patched.Count} method(s):");
            foreach (var m in patched)
            {
                var cls = m.DeclaringType?.FullName ?? "?";
                var name = m is ConstructorInfo ? ".ctor" : m.Name;
                var sig = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                Logger.Info($"[Diagnostic]   - {cls}::{name}({sig})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Diagnostic] GetPatchedMethods failed: {ex}");
        }

        // 诊断 2: 朋友 base game 实际的 StartRunLobby / LoadRunLobby ctor 签名。
        // 如果 dev 反编译版本跟朋友 v0.103.2 不一致，签名会不同 →
        // [HarmonyPatch(MethodType.Constructor, new[] {...})] 没法 bind
        DiagnoseCtorSignatures(typeof(StartRunLobby));
        DiagnoseCtorSignatures(typeof(LoadRunLobby));
    }

    private static void DiagnoseCtorSignatures(Type t)
    {
        try
        {
            var ctors = t.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Logger.Info($"[Diagnostic] {t.FullName} has {ctors.Length} ctor(s):");
            foreach (var c in ctors)
            {
                var sig = string.Join(", ",
                    c.GetParameters().Select(p => p.ParameterType.FullName));
                Logger.Info($"[Diagnostic]   {t.Name}({sig})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Diagnostic] DiagnoseCtorSignatures({t.FullName}) failed: {ex}");
        }
    }
}