using MegaCrit.Sts2.Core.Models;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
///     Encounter list 相邻去重算法。给定一个 encounter 列表，重排让相邻元素的 <c>Id.Entry</c> 尽可能不重复。
///     ## 背景
///     Base game 的 <c>ActModel.GenerateRooms</c> 生成两个全局列表：
///     <c>_rooms.normalEncounters</c> 和 <c>_rooms.eliteEncounters</c>。玩家按消费顺序取：
///     <c>NextNormalEncounter => normalEncounters[normalEncountersVisited % Count]</c>。
///     任何玩家走任何路径，遇到的"第 N 个 Monster"都是同一个 <c>normalEncounters[N]</c>。
///     因此"相邻战斗去重"在这里等价于<b>列表内相邻不重复</b>——不需要按图论 BFS 玩家路径。
///     ## 算法
///     经典"任务调度: 重排相邻字符"问题的贪心解：
///     1. 按 Id.Entry 分组成 multiset
///     2. 每步从所有 group 中选数量最多、且 entry != 上一个的，append 到结果
///     3. 若找不到（剩下全是上一个的副本），fallback 把剩下全部 append（必然产生连续重复）
///     数学事实：当某 entry 频率 > (N+1)/2 时，<b>无法避免</b>相邻重复——这是不可避免的下界，
///     不是算法缺陷。这种情况只发生在池子极小时（如用户把 act1/2 权重设为 0，只留 act3 elite 池，
///     而池里只有 2-3 个 unique encounter 却要填 15 个位置）。
///     复杂度 O(N×K)，K 是 unique entry 数。N=15, K≤9 量级，几十次操作，无性能问题。
///     ## 多人同步
///     算法<b>确定性</b>（无随机、无字典遍历顺序依赖——用 List 保持插入顺序）。各 client 在
///     CustomActEncounterReplacementPatch 之后跑同样的 dedup，结果一致。
/// </summary>
public static class EncounterDeduplicator
{
    /// <summary>
    ///     重排 <paramref name="list" />，让相邻元素的 Id.Entry 尽可能不重复。
    ///     返回值：dedup 后<b>仍然</b>相邻重复的对数（0 = 完美 dedup）。
    /// </summary>
    public static int DeduplicateAdjacent(IList<EncounterModel> list)
    {
        if (list.Count < 2) return 0;

        // 用 List<(key, queue)> 而不是 Dictionary 来保证遍历顺序确定性——多人同步要求确定。
        // queue 保持每个 group 内元素的原始相对顺序（虽然 dedup 后顺序会被打乱，但同 key 元素之间
        // 还是 stable 的）。
        var groupKeys = new List<string>();
        var groupItems = new List<Queue<EncounterModel>>();
        foreach (var e in list)
        {
            var key = e.Id.Entry;
            var idx = groupKeys.IndexOf(key);
            if (idx < 0)
            {
                groupKeys.Add(key);
                groupItems.Add(new Queue<EncounterModel>());
                idx = groupKeys.Count - 1;
            }

            groupItems[idx].Enqueue(e);
        }

        var result = new List<EncounterModel>(list.Count);
        string? lastEntry = null;

        while (true)
        {
            // 选数量最多且 != lastEntry 的 group。
            // 平局时取 groupKeys 中先出现的（按插入顺序），保证 deterministic。
            var bestIdx = -1;
            var bestCount = 0;
            for (var k = 0; k < groupKeys.Count; k++)
            {
                if (groupItems[k].Count == 0) continue;
                if (groupKeys[k] == lastEntry) continue;
                if (groupItems[k].Count > bestCount)
                {
                    bestCount = groupItems[k].Count;
                    bestIdx = k;
                }
            }

            if (bestIdx >= 0)
            {
                result.Add(groupItems[bestIdx].Dequeue());
                lastEntry = groupKeys[bestIdx];
                continue;
            }

            // 找不到 != lastEntry 的——剩下全是 lastEntry 的副本（或没剩了）。
            // 全部 append，结束。剩余元素之间会产生连续重复，但这是数学下界，不可避免。
            var anyRemaining = false;
            for (var k = 0; k < groupKeys.Count; k++)
                while (groupItems[k].Count > 0)
                {
                    result.Add(groupItems[k].Dequeue());
                    anyRemaining = true;
                }

            if (!anyRemaining) break;
            break; // append 完就 break
        }

        // 写回原 list
        for (var i = 0; i < list.Count; i++) list[i] = result[i];

        // 数实际相邻重复对（用于 caller log）
        var dupPairs = 0;
        for (var i = 1; i < list.Count; i++)
            if (list[i].Id.Entry == list[i - 1].Id.Entry)
                dupPairs++;

        return dupPairs;
    }

    /// <summary>
    ///     合并两个 list → 整体 dedup → 按原长度拆回。
    ///     保证：
    ///     - <paramref name="list1" /> 内相邻不重复
    ///     - <paramref name="list2" /> 内相邻不重复
    ///     - list1 末尾 != list2 开头（合并后这俩在 combined 是相邻位置）
    ///     <b>不保证</b>：cross-list 任意路径相邻不重复——玩家路径未知（base game 按 visited 计数独立
    ///     取两个 list 的元素，cross-list 任意 i, j 都可能在玩家路径上相邻），数学上不可解。
    ///     用于 Act5：normalEncounters 和 eliteEncounters 都用 boss 池填充，单独 dedup 不能避免
    ///     玩家从 Monster 节点切到 Elite 节点时看到同一个 boss——合并 dedup 是最大化 unique pattern
    ///     的实用近似。
    /// </summary>
    public static int DeduplicateMerged(IList<EncounterModel> list1, IList<EncounterModel> list2)
    {
        if (list1.Count + list2.Count < 2) return 0;

        var combined = new List<EncounterModel>(list1.Count + list2.Count);
        foreach (var e in list1) combined.Add(e);
        foreach (var e in list2) combined.Add(e);

        var dups = DeduplicateAdjacent(combined);

        for (var i = 0; i < list1.Count; i++) list1[i] = combined[i];
        for (var i = 0; i < list2.Count; i++) list2[i] = combined[list1.Count + i];

        return dups;
    }
}