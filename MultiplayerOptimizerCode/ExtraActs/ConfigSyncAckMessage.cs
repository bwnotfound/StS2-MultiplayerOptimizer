using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// Client 收到 ConfigSyncMessage 并 apply 完后回给 host 的 ack。
///
/// 设计目的：让 host 在 BeginRunForAllPlayers 之前能判断"client 是否真的成功 sync 了配置"。
/// 旧版本 client（没有 EarlyRegisterCustomMessageHandlerPatch）根本收不到 ConfigSyncMessage，
/// 自然也不会回 ack——host 等待超时后即可判定"对方 mod 版本太旧"。
///
/// ShouldBroadcast = false：这条消息只发给 host，不需要广播给其他 client。
/// ShouldBuffer = false：lobby 阶段消息，不能 buffer 到 run 内。
/// </summary>
public sealed class ConfigSyncAckMessage : ICustomMessage
{
    /// <summary>对应 host 发的 ConfigSyncMessage.SyncId。</summary>
    public ulong SyncId { get; set; }

    /// <summary>Client 的 mod 版本号，host 用来检测版本不一致并 log。</summary>
    public string ClientModVersion { get; set; } = "";

    /// <summary>诊断信息：成功 apply 的 double 字段数量。</summary>
    public int AppliedDoubles { get; set; }

    /// <summary>诊断信息：未识别（host 有 client 没）的 double 字段数量。</summary>
    public int SkippedDoubles { get; set; }

    /// <summary>诊断信息：成功 apply 的 bool 字段数量。</summary>
    public int AppliedBools { get; set; }

    /// <summary>诊断信息：未识别的 bool 字段数量。</summary>
    public int SkippedBools { get; set; }

    public bool ShouldBroadcast => false;
    public bool ShouldBuffer => false;
    public NetTransferMode Mode => NetTransferMode.Reliable;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(SyncId);
        writer.WriteString(ClientModVersion);
        writer.WriteInt(AppliedDoubles);
        writer.WriteInt(SkippedDoubles);
        writer.WriteInt(AppliedBools);
        writer.WriteInt(SkippedBools);
    }

    public void Deserialize(PacketReader reader)
    {
        SyncId = reader.ReadULong();
        ClientModVersion = reader.ReadString();
        AppliedDoubles = reader.ReadInt();
        SkippedDoubles = reader.ReadInt();
        AppliedBools = reader.ReadInt();
        SkippedBools = reader.ReadInt();
    }

    /// <summary>
    /// Host 端收到 ack 后调用——记录到 ConfigSyncManager 的 pending tracker。
    /// 非 host（包括其他 client 错误广播过来的）应当忽略。
    /// </summary>
    public void HandleMessage(ulong senderId)
    {
        if (!ConfigSyncManager.IsLocalHost()) return;
        ConfigSyncManager.RecordAck(this, senderId);
    }
}