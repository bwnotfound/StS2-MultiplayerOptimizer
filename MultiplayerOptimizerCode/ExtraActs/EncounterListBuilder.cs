using System.Collections.Generic;
using MegaCrit.Sts2.Core.Random;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 把一个 encounter list（normalEncounters / eliteEncounters）按多样性策略填充到指定长度。
///
/// 当前策略：shuffle-bag
///   - 每耗尽一遍 pool 后重新 shuffle，保证短期内 pool 中元素不重复
///   - 池子大小 N 时，相邻 N-1 个位置的 encounter 都不同
///   - 池子越大，多样性越好；池子小于目标长度时仍然会循环重复，这是池容量限制不是算法问题
///
/// 路径多样性：地图是多分支的，但 RoomSet.NextNormalEncounter 是按访问次数线性消费 list。
/// 玩家走任何路径都消费这个 list 的连续片段，shuffle-bag 保证了一条路径上短期不重复——
/// 这等价于"路径上多样性"。
///
/// 后续可在此类增加：
///   - WeightedFill：池子里元素带权重（来自 step 4 的混合池权重配置）
///   - PathAwareFill：感知 map 拓扑做更精细的多样性（一般用不到）
/// </summary>
internal static class EncounterListBuilder
{
    /// <summary>
    /// 把 destination 清空后填充 targetCount 个元素，元素来自 pool。
    /// </summary>
    public static void FillWithShuffleBag<T>(
        List<T> destination,
        IReadOnlyList<T> pool,
        int targetCount,
        Rng rng) where T : class
    {
        destination.Clear();
        if (pool.Count == 0 || targetCount == 0) return;

        var bag = new List<T>();
        while (destination.Count < targetCount)
        {
            if (bag.Count == 0)
            {
                bag.AddRange(pool);
                ShuffleInPlace(bag, rng);
            }

            int last = bag.Count - 1;
            destination.Add(bag[last]);
            bag.RemoveAt(last);
        }
    }

    /// <summary>
    /// Fisher-Yates 洗牌。
    /// </summary>
    private static void ShuffleInPlace<T>(IList<T> list, Rng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}