using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Runs;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

/// <summary>
/// 直接读写 <c>RunState._currentActIndex</c> 底层私有字段，<b>绕过</b>
/// <c>IRunState.CurrentActIndex</c> property 的 setter。
///
/// ## 为什么必须绕过 setter
///
/// base game <c>RunState.CurrentActIndex</c> 的 setter（src/Core/Runs/RunState.cs:43）：
/// <code>
///   set
///   {
///       if (_currentActIndex != value)
///       {
///           _visitedMapCoords.Clear();   // 清空已访问地图坐标
///           ActFloor = 0;                // act 楼层归零
///           NextRoomId = 0;              // 房间 ID 归零
///           _currentActIndex = value;
///       }
///   }
/// </code>
///
/// setter 在值改变时有<b>破坏性副作用</b>——它假设"CurrentActIndex 改变 = 进入了新的 act，
/// 所以要把 act 内的进度全部清零"。
///
/// 任何 mod 想做"临时把 CurrentActIndex 改成别的值、用完再改回来"——如果走 property setter，
/// 会触发<b>两次</b>副作用（改过去一次、改回来一次），把当前 act 的 <c>_visitedMapCoords</c>
/// 彻底清空。后果：
///   - <c>CurrentMapCoord</c>（= <c>_visitedMapCoords.Last()</c>）变成 null
///   - <c>ActFloor</c> / <c>NextRoomId</c> 归零（"玩家被送回 act 开始"）
///   - 地图导航拿不到当前坐标 → 选下一个节点时卡死
///
/// 这个 bug 实际发生过：早期的 RestSiteCharacterCustomActPatch 用 property setter 临时改
/// CurrentActIndex 来规避 NRestSiteCharacter._Ready 的 act switch，结果每次进 act4/5 篝火
/// 都把地图进度清空。
///
/// ## 正确做法
///
/// 直接反射写 <c>_currentActIndex</c> 字段。它只是个 int 字段，直接赋值不触发任何副作用——
/// 等价于"我就是想改这个数字，别动其它任何东西"。
///
/// 读取不需要绕过——<c>CurrentActIndex</c> 的 getter 只是 <c>return _currentActIndex</c>，
/// 无副作用，直接用 property getter 即可。本类只负责<b>写</b>。
///
/// ## 失败处理
///
/// 如果反射找不到字段（base game 重命名了字段），<see cref="WriteRaw"/> 返回 false。
/// 调用方必须检查返回值：失败时应当<b>放弃改值</b>，绝不退回到 property setter——
/// 宁可让依赖改值的功能失效（比如篝火 _Ready 重新抛 "Unexpected act"），也不能用 setter
/// 污染存档。
/// </summary>
internal static class RunStateActIndexWriter
{
    private static FieldInfo? _field;
    private static bool _lookupDone;

    private static FieldInfo? GetField(IRunState runState)
    {
        if (_lookupDone) return _field;
        _lookupDone = true;
        try
        {
            // 用实际运行时类型（正常 run 内是 RunState）查私有字段
            _field = runState.GetType().GetField(
                "_currentActIndex", BindingFlags.NonPublic | BindingFlags.Instance);

            if (_field == null)
            {
                MainFile.Logger.Error(
                    "RunStateActIndexWriter: RunState._currentActIndex field not found. " +
                    "Act-index-dependent patches will be disabled to avoid corrupting the run state. " +
                    "(base game may have renamed the field — please report.)");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"RunStateActIndexWriter: field lookup failed: {ex}");
        }

        return _field;
    }

    /// <summary>
    /// 直接把 <c>_currentActIndex</c> 字段写成 <paramref name="value"/>，绕过 setter 副作用。
    /// 返回是否成功。失败时调用方应放弃改值（绝不退回 property setter）。
    /// </summary>
    public static bool WriteRaw(IRunState? runState, int value)
    {
        if (runState == null) return false;

        var field = GetField(runState);
        if (field == null) return false;

        try
        {
            field.SetValue(runState, value);
            return true;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"RunStateActIndexWriter.WriteRaw failed: {ex}");
            return false;
        }
    }
}