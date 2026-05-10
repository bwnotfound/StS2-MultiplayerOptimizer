using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Random;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode.ExtraActs;

/// <summary>
/// 把 encounter list（normalEncounters / eliteEncounters）按多样性策略填充到指定长度。
///
/// 提供两种填充模式：
///   - <see cref="FillWithShuffleBag"/>：单池 shuffle-bag（短期不重复）
///   - <see cref="FillWithWeightedPools"/>：多池加权混合 + 全局 shuffle-bag
///
/// 公开 helper：
///   - <see cref="BuildWeightedFlatList"/>：构造按权重展开的扁平 list
///     （Act4Model.GenerateAllEncounters 用它生成"权重重复"的 boss encounter，
///      让 act.AllBossEncounters 自带权重，rng.NextItem 等价加权抽样）
/// </summary>
internal static class EncounterListBuilder
{
    /// <summary>
    /// 单池 shuffle-bag 填充：每耗尽一遍 pool 后重新 shuffle。
    /// 池子大小 N 时，相邻 N-1 个位置的 encounter 都不同。
    /// </summary>
    public static void FillWithShuffleBag<T>(
        List<T> destination, IReadOnlyList<T> pool, int targetCount, Rng rng) where T : class
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

            var last = bag.Count - 1;
            destination.Add(bag[last]);
            bag.RemoveAt(last);
        }
    }

    /// <summary>
    /// 多池加权填充：每个池有自己的权重，最终 list 中各池元素出现频率正比于权重。
    ///
    /// 算法：构造全局 weighted bag（每个元素按权重重复添加），shuffle 后线性消费；
    /// bag 耗尽后重 shuffle 再用，确保 destination 长度 > bag 长度时仍有多样性。
    /// </summary>
    public static void FillWithWeightedPools<T>(
        List<T> destination,
        int targetCount,
        IReadOnlyList<(IReadOnlyList<T> pool, double weight)> weightedPools,
        Rng rng,
        int baseFactor = 100) where T : class
    {
        destination.Clear();
        if (targetCount == 0) return;

        var bag = BuildWeightedFlatList(weightedPools, baseFactor);
        if (bag.Count == 0) return;

        ShuffleInPlace(bag, rng);
        var idx = 0;
        while (destination.Count < targetCount)
        {
            if (idx >= bag.Count)
            {
                ShuffleInPlace(bag, rng);
                idx = 0;
            }

            destination.Add(bag[idx++]);
        }
    }

    /// <summary>
    /// 构造按权重展开的扁平 list。
    ///
    /// 例如 weightedPools = [(act1池8个, 0.2), (act2池7个, 0.3), (act3池11个, 0.5)], baseFactor=100：
    ///   - act1: share = 0.2/1.0 * 100 = 20，每元素 round(20/8) = 3 次 → 24 entries
    ///   - act2: share = 30，每元素 round(30/7) ≈ 4 次 → 28 entries
    ///   - act3: share = 50，每元素 round(50/11) ≈ 5 次 → 55 entries
    ///   - 总 107 entries（未 shuffle）
    ///
    /// 重复次数下限是 1（防止权重很小但 pool 很大时元素被舍掉）。
    /// </summary>
    public static List<T> BuildWeightedFlatList<T>(
        IReadOnlyList<(IReadOnlyList<T> pool, double weight)> weightedPools,
        int baseFactor = 100)
    {
        var bag = new List<T>();

        double totalWeight = 0;
        foreach (var (pool, w) in weightedPools)
            if (pool.Count > 0 && w > 0)
                totalWeight += w;

        if (totalWeight <= 0) return bag;

        foreach (var (pool, weight) in weightedPools)
        {
            if (pool.Count == 0 || weight <= 0) continue;
            var share = weight / totalWeight * baseFactor;
            var repeats = Math.Max(1, (int)Math.Round(share / pool.Count));
            for (var i = 0; i < repeats; i++)
                bag.AddRange(pool);
        }

        return bag;
    }

    private static void ShuffleInPlace<T>(IList<T> list, Rng rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}