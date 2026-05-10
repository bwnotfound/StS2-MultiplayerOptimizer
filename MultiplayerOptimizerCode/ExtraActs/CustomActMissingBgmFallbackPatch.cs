using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// act4/5 战斗 BGM fallback：boss 自己的 CustomBgm event 不在当前 act 加载的 audio bank 内时，
/// 替换为兜底 boss bgm，避免 audio engine 报 "missing music track" 然后没声音。
///
/// 背景：
///   audio bank 加载是 act-specific 且每次只加载一个：
///     NRunMusicController._OnReady 调 LoadActBank(musicBankPaths[num])
///     → fmod proxy.Call("load_act_banks", [bankPath])
///   每个 ActModel.MusicBankPaths 列出 a1/a2 两 bank，运行期 rng 选一个加载，其他不在内存。
///
///   每个 boss EncounterModel.CustomBgm 写死自己的 event path，例如：
///     VantomBoss → "event:/music/act1_boss_vantom"
///     KnowledgeDemonBoss → "event:/music/act2_boss_knowledge_demon"
///     QueenBoss → "event:/music/act3_boss_queen"
///   战斗开始 CombatManager 调 NRunMusicController.PlayCustomMusic(encounter.CustomBgm)。
///
/// 我们的 mod 让 act4/5 final boss 池可以来自所有 act，但 act4 复用 Hive 的 bank（act2_*），
/// act5 复用 Glory 的 bank（act3_*）。当 act4 final boss 抽到 vantom（act1）时，
/// `act1_boss_vantom` event 不在 act2 bank 里 → audio engine 找不到 → 无声 + log error。
///
/// 修法：patch PlayCustomMusic 的 prefix，对 act4/5 战斗，如果 customMusic 不是 boss event
/// 或者属于当前 bank 已知含有的 entry，直接放行；否则 fallback 到当前 act bank 内已知能播的
/// 一首 boss bgm。
///
/// fallback 选曲：
///   - act4 (Hive bank, act2_*) → act2_boss_knowledge_demon
///   - act5 (Glory bank, act3_*) → act3_boss_queen
/// 这样所有 act4 final boss 都用 KnowledgeDemon 的 bgm，所有 act5 final boss 都用 Queen 的 bgm。
/// 丢失了"boss 各有专属 bgm"的体验，但保证有声。如果未来发现某 boss bgm 也在 bank 内但被这个
/// patch 误杀，可以加到 BankExtras 白名单。
/// </summary>
[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.PlayCustomMusic))]
public static class CustomActMissingBgmFallbackPatch
{
    // act4 (Hive bank) 兜底曲：KnowledgeDemonBoss 的 bgm，必在 act2 bank 内
    private const string Act4FallbackTrack = "event:/music/act2_boss_knowledge_demon";

    // act5 (Glory bank) 兜底曲：QueenBoss 的 bgm，必在 act3 bank 内
    private const string Act5FallbackTrack = "event:/music/act3_boss_queen";

    // act2 bank 已知含有的"非 act2_ 前缀"entry。CeremonialBeastBoss 是 act2 的 boss
    // 但 CustomBgm 写的是 act1_boss_ceremonial_beast —— act2 bank 里有这个 entry。
    private static readonly HashSet<string> Act2BankCrossActExtras = new()
    {
        "event:/music/act1_boss_ceremonial_beast"
    };

    [HarmonyPrefix]
    public static void Prefix(ref string customMusic)
    {
        if (string.IsNullOrEmpty(customMusic)) return;

        // 只关心 boss bgm —— 含"_boss_"段的才介入。其他 PlayCustomMusic 调用（事件/特殊曲）不动。
        if (!customMusic.Contains("_boss_")) return;

        var rm = RunManager.Instance;
        if (rm == null) return;
        var state = RunStateAccessor.GetState(rm);
        if (state == null) return;

        if (state.Act is Act4Model)
        {
            // act4 用 act2_a1 / act2_a2 bank（Hive 的 MusicBankPaths）
            if (customMusic.StartsWith("event:/music/act2_")) return;
            if (Act2BankCrossActExtras.Contains(customMusic)) return;
            // 其他都 fallback
            MainFile.Logger.Info(
                $"[ExtraActs] act4 boss bgm fallback: '{customMusic}' not in act2 bank, " +
                $"replacing with '{Act4FallbackTrack}'");
            customMusic = Act4FallbackTrack;
        }
        else if (state.Act is Act5Model)
        {
            // act5 用 act3_a1 / act3_a2 bank（Glory 的 MusicBankPaths）
            if (customMusic.StartsWith("event:/music/act3_")) return;
            // 其他都 fallback
            MainFile.Logger.Info(
                $"[ExtraActs] act5 boss bgm fallback: '{customMusic}' not in act3 bank, " +
                $"replacing with '{Act5FallbackTrack}'");
            customMusic = Act5FallbackTrack;
        }
    }
}