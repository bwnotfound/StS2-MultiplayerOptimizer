// ReSharper disable InconsistentNaming
// 抑制 IDE 关于 __instance 的命名规则警告：__instance 是 Harmony 强制约定，
// 改成 instance 会导致 PatchAll 抛 "Parameter 'instance' not found"，整个 mod 加载失败。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 共享的 ConfigSync 流程，被 StartRunLobby 和 LoadRunLobby 两条路径共用。
///
/// ## 整体流程
///   1. host 进入"即将 broadcast LobbyBegin* 消息开 run"的方法
///   2. 我们的 prefix 介入：
///       a. 注册 pending ack 集合（所有 client id）
///       b. broadcast ConfigSyncMessage
///       c. 启动异步 task：await ack 完成 → 回主线程 → 反射调原方法
///       d. prefix return false 跳过原方法
///   3. 异步等待结果：
///       - 全部 ack 成功 → log + 反射 Invoke 原方法 → 正常开 run
///       - 超时（任何一个 client 没 ack）→ popup + 不调原方法 → lobby 卡 ready
///
/// ## ConfigSyncManager.IsReenteringBeginRun
/// 标记当前是异步 task 触发的二次调用（用反射跑原方法时）。这次进入 prefix 时直接放行，
/// 避免递归触发 sync。两条 lobby 路径共用同一个 flag——OK 因为同时只可能有一条路径在跑。
/// </summary>
internal static class ConfigSyncFlow
{
    /// <summary>等 client ack 的超时（毫秒）。3 秒足够正常网络传输 + apply。</summary>
    public const int AckTimeoutMs = 3000;

    /// <summary>
    /// Host 端入口：broadcast 配置 + 异步等 ack + 完成后回调 originalInvoker。
    ///
    /// 返回 true 表示"放行原方法"（client/单人/无 client/重入 case），调用方的 prefix 应 return true。
    /// 返回 false 表示"已启动异步流程，跳过原方法"，调用方的 prefix 应 return false。
    /// </summary>
    public static bool StartSyncOrPassthrough(
        INetGameService netService,
        IReadOnlyCollection<ulong> connectedPlayerIds,
        Action invokeOriginalOnSuccess)
    {
        // 异步 task 触发的二次调用——直接放行原方法
        if (ConfigSyncManager.IsReenteringBeginRun) return true;

        // client / 单人模式：原方法自己会判断 NetGameType.Client 抛错 / 不发消息
        if (netService.Type != NetGameType.Host) return true;

        // 收集所有非 host 的 client id
        var hostId = netService.NetId;
        var clientIds = connectedPlayerIds.Where(id => id != hostId).ToList();

        if (clientIds.Count == 0)
        {
            // host 自己一个人——跳过 sync 直接开 run
            return true;
        }

        // 注册 pending + broadcast sync
        var msg = ConfigSyncMessage.CaptureCurrent();
        msg.SyncId = ConfigSyncManager.RegisterPending(clientIds);
        netService.SendMessage(new CustomMessageWrapper { Message = msg });

        MainFile.Logger.Info(
            $"[Sync] Host broadcasting config (syncId={msg.SyncId}, version={msg.HostModVersion}) " +
            $"to {clientIds.Count} clients ({msg.Doubles.Count}D+{msg.Bools.Count}B), waiting acks...");

        // 启动异步等 ack，完成后回主线程做后续工作
        var mainCtx = SynchronizationContext.Current;
        TaskHelper.RunSafely(WaitAcksAndContinueAsync(msg.SyncId, invokeOriginalOnSuccess, mainCtx));

        return false;
    }

    private static async Task WaitAcksAndContinueAsync(
        ulong syncId,
        Action invokeOriginalOnSuccess,
        SynchronizationContext? mainCtx)
    {
        var result = await ConfigSyncManager.WaitForAcksAsync(syncId, AckTimeoutMs);

        void DoOnMainThread()
        {
            if (!result.AllAcked)
            {
                var missing = string.Join(", ", result.MissingClients);
                MainFile.Logger.Error(
                    $"[Sync] Config sync FAILED (syncId={syncId}): timeout waiting for ack from clients [{missing}]. " +
                    "These clients likely have an outdated mod version (no EarlyRegister patch). Run will NOT start.");

                ShowIncompatibilityPopup(result.MissingClients);
                return;
            }

            // 检查版本一致性
            var versionMismatches = result.ReceivedAcks
                .Where(kv => kv.Value.ClientModVersion != MainFile.ModVersion)
                .ToList();

            if (versionMismatches.Count > 0)
            {
                foreach (var (id, ack) in versionMismatches)
                {
                    MainFile.Logger.Warn(
                        $"[Sync] Client {id} version='{ack.ClientModVersion}' differs from host='{MainFile.ModVersion}'. " +
                        $"Applied {ack.AppliedDoubles}D+{ack.AppliedBools}B / Skipped {ack.SkippedDoubles}D+{ack.SkippedBools}B. " +
                        "Run will start but state divergence is possible during gameplay.");
                }
            }
            else
            {
                MainFile.Logger.Info(
                    $"[Sync] All {result.ReceivedAcks.Count} clients acked sync " +
                    $"(syncId={syncId}, version={MainFile.ModVersion}). Proceeding to begin run.");
            }

            // 调用原方法 via 调用方提供的 invoker（每条路径反射调不同的 private method）
            ConfigSyncManager.IsReenteringBeginRun = true;
            try
            {
                invokeOriginalOnSuccess();
            }
            catch (Exception ex)
            {
                MainFile.Logger.Error($"[Sync] Failed to invoke original method after sync: {ex}");
            }
            finally
            {
                ConfigSyncManager.IsReenteringBeginRun = false;
            }
        }

        if (mainCtx != null)
            mainCtx.Post(_ => DoOnMainThread(), null);
        else
            DoOnMainThread(); // fallback: 没捕获到 ctx 就直接跑（可能不在主线程，UI 操作可能 warn）
    }

    /// <summary>注册 wrapper handler 的共享逻辑——StartRunLobby 和 LoadRunLobby 各自的 patch 调用此方法。</summary>
    public static void RegisterCustomMessageHandler(INetGameService netService, string lobbyKind)
    {
        netService.RegisterMessageHandler<CustomMessageWrapper>(EarlyHandleCustomMessage);

        // 关键: 让 ConfigSyncManager 拿到 NetService 引用，否则 lobby 阶段的 SendAck / IsLocalHost
        // 用 RunManager.NetService（还是 null）取不到 NetService，sync 会全程失败
        ConfigSyncManager.SetActiveNetService(netService);

        MainFile.Logger.Info(
            $"[Sync] Early-registered CustomMessageWrapper handler in {lobbyKind} ctor " +
            $"(NetService.Type={netService.Type})");
    }

    private static void EarlyHandleCustomMessage(CustomMessageWrapper wrapper, ulong senderId)
    {
        // 跟 BaseLib 的 HandleCustomMessage 行为一致——直接转发给 ICustomMessage 实现
        wrapper.Message.HandleMessage(senderId);
    }

    private static void ShowIncompatibilityPopup(IReadOnlyCollection<ulong> missingClients)
    {
        var clientList = string.Join("\n", missingClients.Select(id => $"  • {id}"));
        var title = "Mod 版本不兼容";
        var body =
            $"以下玩家的 MultiplayerOptimizer mod 版本太旧，无法同步配置：\n\n{clientList}\n\n" +
            $"主机版本：{MainFile.ModVersion}\n\n" +
            "请让对方升级到与主机相同的 mod 版本后再开始游戏。";

        var popup = NErrorPopup.Create(title, body, showReportBugButton: false);
        if (popup != null)
        {
            NModalContainer.Instance?.Add(popup);
        }
        else
        {
            // popup 创建失败（test mode 之类）退化为 log
            MainFile.Logger.Error($"[Sync] {title}: {body}");
        }
    }
}

// ============================================================
// EarlyRegisterCustomMessageHandlerPatch（两条 lobby 路径各一份）
// ============================================================

/// <summary>
/// 让 StartRunLobby 构造时就注册 CustomMessageWrapper handler。
/// 这样 lobby 期 host 发来的 ConfigSyncMessage / ConfigSyncAckMessage 能立刻被处理。
///
/// ## 为什么需要
/// BaseLib 默认在 RunManager.InitializeShared postfix 才注册 wrapper handler——那时已经在
/// BeginRunLocally 之后。client 在 lobby 期收到的 ConfigSync 消息没 handler，会被丢弃。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor,
    new[] { typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int) })]
public static class EarlyRegisterHandlerForStartLobbyPatch
{
    [HarmonyPostfix]
    public static void Postfix(StartRunLobby __instance)
    {
        ConfigSyncFlow.RegisterCustomMessageHandler(__instance.NetService, "StartRunLobby");
    }
}

/// <summary>
/// 同 EarlyRegisterHandlerForStartLobbyPatch，但用于 LoadRunLobby（加载存档继续玩的入口）。
/// 没这个 patch 的话 LoadRunLobby 路径上 host 端的 sync broadcast 也收不到 client ack。
/// </summary>
[HarmonyPatch(typeof(LoadRunLobby), MethodType.Constructor,
    new[] { typeof(INetGameService), typeof(ILoadRunLobbyListener), typeof(SerializableRun) })]
public static class EarlyRegisterHandlerForLoadLobbyPatch
{
    [HarmonyPostfix]
    public static void Postfix(LoadRunLobby __instance)
    {
        ConfigSyncFlow.RegisterCustomMessageHandler(__instance.NetService, "LoadRunLobby");
    }
}

// ============================================================
// HostBroadcastConfig（两条 lobby 路径各一份）
// ============================================================

/// <summary>
/// Host 端：开新 run 时在广播 LobbyBeginRunMessage 之前 sync 配置 + 等 ack。
///
/// Patch StartRunLobby.BeginRunForAllPlayers (private, async-ish 但实际是同步 void)。
/// 详见 <see cref="ConfigSyncFlow"/> 注释的整体流程。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
public static class HostBroadcastConfigOnStartPatch
{
    private static MethodInfo? _originalMethod;

    private static MethodInfo OriginalMethod => _originalMethod ??= AccessTools.Method(
        typeof(StartRunLobby), "BeginRunForAllPlayers",
        new[] { typeof(string), typeof(List<ModifierModel>) });

    [HarmonyPrefix]
    public static bool Prefix(
        StartRunLobby __instance,
        string seed,
        List<ModifierModel> modifiers)
    {
        return ConfigSyncFlow.StartSyncOrPassthrough(
            __instance.NetService,
            __instance.Players.Select(p => p.id).ToList(),
            // ack 成功后回调：用反射调原 BeginRunForAllPlayers
            () => OriginalMethod.Invoke(__instance, new object[] { seed, modifiers }));
    }
}

/// <summary>
/// Host 端：加载存档继续玩时在广播 LobbyBeginLoadedRunMessage 之前 sync 配置 + 等 ack。
///
/// 选择 patch BeginRunIfAllPlayersReady（同步 void）而不是 TryBeginRun（async Task）——
/// async 方法的 Harmony prefix 行为不直观（state machine entry 比较绕），同步方法更稳定。
///
/// BeginRunIfAllPlayersReady 在每次 SetReady 时调用。它会先检查 IsAboutToBeginGame()——
/// 只有所有连接玩家都 ready 时才真正 TryBeginRun。我们的 prefix 只在 IsAboutToBeginGame()
/// 时才介入 sync，否则放行让原方法做正常的 no-op。
/// </summary>
[HarmonyPatch(typeof(LoadRunLobby), "BeginRunIfAllPlayersReady")]
public static class HostBroadcastConfigOnLoadPatch
{
    private static MethodInfo? _originalMethod;

    private static MethodInfo OriginalMethod => _originalMethod ??= AccessTools.Method(
        typeof(LoadRunLobby), "BeginRunIfAllPlayersReady");

    [HarmonyPrefix]
    public static bool Prefix(LoadRunLobby __instance)
    {
        // 只有"所有连接玩家都 ready 即将开 run"时介入 sync；否则放行让原方法做 no-op
        if (!__instance.IsAboutToBeginGame()) return true;

        return ConfigSyncFlow.StartSyncOrPassthrough(
            __instance.NetService,
            __instance.ConnectedPlayerIds,
            // ack 成功后回调：用反射调原 BeginRunIfAllPlayersReady
            () => OriginalMethod.Invoke(__instance, null));
    }
}

// ============================================================
// 共用 RestoreConfigOnRunEnd
// ============================================================

/// <summary>
/// 任意 run 结束（胜利/失败/退出/断线）→ 恢复 client 原配置。
/// RunManager.CleanUp 是 RunManager 的统一清理入口，所有 run 结束路径都会跑。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
public static class RestoreConfigOnRunEndPatch
{
    [HarmonyPostfix]
    public static void RestoreAfterRunEnd()
    {
        // ConfigSyncManager.Restore 内部会检查 IsActive：
        //   - host 端 / 单机端：IsActive=false，无操作
        //   - client 端：IsActive=true，从磁盘 reload 恢复原配置
        ConfigSyncManager.Restore();
    }
}