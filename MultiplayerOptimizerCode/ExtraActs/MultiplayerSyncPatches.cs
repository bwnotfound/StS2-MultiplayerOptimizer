using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// Lobby 创建时就提前注册 CustomMessageWrapper 处理器。
///
/// ## 问题
/// BaseLib 默认在 RunManager.InitializeShared postfix 才注册 CustomMessageWrapper handler
/// （CustomMessagePatches.cs:11-17）。但 STS2 的多人 client 端流程：
///
///   收到 LobbyBeginRunMessage
///     → handler 同步部分调 BeginRunLocally → LobbyListener.BeginRun
///       → TaskHelper.RunSafely(StartNewMultiplayerRun(...))    ← 异步启动！
///     → handler 立即返回
///   network thread 立即处理下一条消息（我们的 ConfigSyncMessage）
///     → wrapper handler 还没注册（异步任务还没跑到 InitializeShared）
///     → NetMessageBus.cs:61 "Received message of type ..., but no message handlers are registered"
///     → **直接丢弃**（base game 没有 buffer 机制）
///
/// reliable+ordered 也救不了——它只保证消息按顺序*到达*，但 client 收到 ConfigSync 时
/// 还是 lobby 阶段（不是 in-run），handler 还没注册。
///
/// ## 修复
/// Patch StartRunLobby 构造函数 postfix——lobby 创建时（远早于 RunManager 初始化）就在
/// 同一个 NetService 上注册我们自己的 wrapper handler。这样 client 收到 lobby 期发的
/// ConfigSync 时已有 handler，ConfigSync.HandleMessage 被正常调用，apply config 到本地静态字段。
///
/// 后续 BaseLib 在 InitializeShared 注册的另一份 wrapper handler 跟我们这份并存——
/// 一条消息触发两次 HandleMessage——但 ConfigSyncManager.Apply 是 idempotent（重复 apply
/// 同一份数据无副作用），不会出问题。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor,
    new[] { typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int) })]
public static class EarlyRegisterCustomMessageHandlerPatch
{
    [HarmonyPostfix]
    public static void Postfix(StartRunLobby __instance)
    {
        __instance.NetService.RegisterMessageHandler<CustomMessageWrapper>(EarlyHandleCustomMessage);
        MainFile.Logger.Info(
            $"[Sync] Early-registered CustomMessageWrapper handler in StartRunLobby ctor " +
            $"(NetService.Type={__instance.NetService.Type})");
    }

    private static void EarlyHandleCustomMessage(CustomMessageWrapper wrapper, ulong senderId)
    {
        // 跟 BaseLib 的 HandleCustomMessage 行为一致——直接转发给 ICustomMessage 实现
        wrapper.Message.HandleMessage(senderId);
    }
}

/// <summary>
/// Host 端：在广播 LobbyBeginRunMessage 之前先 broadcast 自己的 mod 配置，让所有 client
/// 在 BeginRunLocally 之前完成 config sync。
///
/// 配合 <see cref="EarlyRegisterCustomMessageHandlerPatch"/> 使用——后者保证 client 在 lobby
/// 期就有 wrapper handler，能正常收到这条消息。
///
/// 时序保证：传输是 reliable + ordered，host 在 BeginRunForAllPlayers 内连续 SendMessage：
///   1. （我们的 prefix）broadcast ConfigSyncMessage
///   2. （原方法 line 399）broadcast LobbyBeginRunMessage
/// client 一定按顺序收到 1 → 2。1 触发 ConfigSyncManager.Apply 到本地静态字段；
/// 2 触发 BeginRunLocally → SetUpNewMultiPlayer → GenerateRooms（此时读 config 已是 host 值）。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
public static class HostBroadcastConfigPatch
{
    [HarmonyPrefix]
    public static void BroadcastBeforeBeginRun(StartRunLobby __instance)
    {
        // 单机 NetSingleplayerGameService 的 SendMessage 是 no-op，跳过省事
        // Client 不会调 BeginRunForAllPlayers（line 382-385 throw），保险起见也跳过
        if (__instance.NetService.Type != NetGameType.Host) return;

        var msg = ConfigSyncMessage.CaptureCurrent();
        var wrapper = new CustomMessageWrapper { Message = msg };
        __instance.NetService.SendMessage(wrapper);

        MainFile.Logger.Info(
            $"[Sync] Host broadcasting config to clients " +
            $"({msg.Doubles.Count} doubles + {msg.Bools.Count} bools)");
    }
}

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