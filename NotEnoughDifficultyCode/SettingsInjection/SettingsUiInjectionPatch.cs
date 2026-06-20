using System;
using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
/// 把 mod 的"启用额外加速"和"额外加速倍率"两个设置注入到官方游戏设置界面，紧贴 FastMode row 之后。
///
/// ## 实现策略（参考 SteamRandomMatch mod）
///
/// base game 没暴露任何 mod-API 注入 settings UI，但 settings 界面是 Godot scene tree——
/// 我们可以在 <c>NSettingsScreen._Ready</c> postfix 时 duplicate 已有的 row 作为模板：
///   - tickbox 模板：<c>UploadGameplayData</c>（NUploadDataTickbox，最简单的 tickbox row）
///   - slider 模板：<c>BgmVolume</c>（NBgmVolumeSlider，跨面板复制）
///
/// 复制 row → 改 Name（独特标识，用于后续行为路由）→ 改 label 文本 → 重置控件值/handler。
///
/// ## 控件行为重定向
///
/// duplicate 后的 row 仍是 NUploadDataTickbox / NBgmVolumeSlider 实例，原本会改 base game
/// PrefsSave 字段。两种策略：
///   - <b>Tickbox</b>：OnTick/OnUntick/SetFromSettings 是 C# virtual override，无法
///     disconnect。改用 Harmony patch prefix，按 row Name 路由——见 InjectedTickboxRouterPatch。
///   - <b>Slider</b>：ValueChanged 是 Godot signal，可以 Disconnect + Connect 新 handler，
///     完全本地处理。
///
/// ## 跟 mod 配置界面的关系
///
/// mod 配置（BaseLib ConfigUI 自动生成的）里仍然有 EnableSpeedMultiplier / SpeedMultiplier
/// 控件——它们和官方 settings 里注入的控件共同写同一个 static 字段。
/// SpeedMultiplierController._Process 每帧 poll config 同步到 Engine.TimeScale，所以任一处改
/// 都立即生效。两处显示不会自动同步（一个改了另一个 UI 看到的还是旧值），但**功能上完全等价**。
///
/// ## Idempotency
///
/// _Ready 可能被调多次（场景刷新等）。每次注入前检查新 row 是否已存在（用 Name 查），存在则跳过。
/// Slider 用 Meta flag 防止 handler 重复 connect。
///
/// ## 失败处理
///
/// 整段包 try/catch：注入失败只是 UI 没出现，mod 其他功能（patches、speed controller）继续工作。
/// base game scene 升级改了节点名字时会失效——但只是降级到"用户从 mod 配置改"的体验，不破坏游戏。
/// </summary>
[HarmonyPatch(typeof(NSettingsScreen), "_Ready")]
internal static class SettingsUiInjectionPatch
{
    private const string TickboxTemplateName = "UploadGameplayData";
    private const string SliderTemplateName = "BgmVolume";
    private const string FastModeRowName = "FastMode";

    /// <summary>Meta key：标记 slider 已经被我们 disconnect + reconnect 过，避免重复处理。</summary>
    private const string SliderHookedMetaKey = "mo_extra_speed_slider_hooked";

    /// <summary>
    /// SpeedMultiplier 范围 0.5-10，UI slider 用 5-100 整数（×10 表示）便于 step 控制。
    /// 显示时 ÷10 还原成 "X.Yx" 字符串。
    /// </summary>
    private const double SliderMinInternal = 5.0; // = 0.5x

    private const double SliderMaxInternal = 100.0; // = 10.0x
    private const double SliderStepInternal = 1.0; // = 0.1x increments
    private const double InternalToDisplay = 10.0;

    [HarmonyPostfix]
    private static void Postfix(NSettingsScreen __instance)
    {
        if (!PatchScope.IsEnabled) return;

        try
        {
            InjectRows(__instance);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Settings UI injection failed: {ex}");
        }
    }

    /// <summary>
    /// 注入或刷新 row。两个时机被调用：
    ///   - <see cref="Postfix(NSettingsScreen)"/>：NSettingsScreen._Ready 首次构造时（注入新 row）
    ///   - <see cref="SettingsUiOnSubmenuShownRefreshPatch.Postfix(NSettingsScreen)"/>：
    ///     每次切回官方设置界面时（刷新已注入的 row，让模组配置 UI 的改动反映过来）
    ///
    /// internal 而非 private——让 sibling patch class 能调用。
    /// </summary>
    internal static void InjectRows(NSettingsScreen screen)
    {
        // 1. 拿 GeneralSettings 面板的 Content 容器（VBoxContainer）
        var generalPanel = screen.GetNodeOrNull<NSettingsPanel>("%GeneralSettings");
        if (generalPanel == null)
        {
            MainFile.Logger.Warn("Settings injection: %GeneralSettings panel not found");
            return;
        }

        var content = generalPanel.Content;

        // 2. 找插入位置参考点：FastMode row
        var fastModeRow = content.GetNodeOrNull<Control>(FastModeRowName);
        if (fastModeRow == null)
        {
            MainFile.Logger.Warn("Settings injection: FastMode row not found, can't determine insert position");
            return;
        }

        var insertAfterIndex = fastModeRow.GetIndex();

        // 3. Tickbox row 注入（在 FastMode 之后第一格）
        var existingTickboxRow = content.GetNodeOrNull<Control>(InjectedRowKind.EnableExtraSpeedRowName);
        if (existingTickboxRow == null)
        {
            var tickboxTemplate = content.GetNodeOrNull<Control>(TickboxTemplateName);
            if (tickboxTemplate == null)
            {
                MainFile.Logger.Warn($"Settings injection: {TickboxTemplateName} row not found in GeneralSettings");
            }
            else
            {
                InjectTickboxRow(content, tickboxTemplate, insertAfterIndex + 1);
            }
        }
        else
        {
            // 已存在：触发 SetFromSettings 把当前 config 值同步到 UI
            RefreshTickboxesInRow(existingTickboxRow);
            // 重设 label 文本：首次创建时若 mod loc 表尚未合并进游戏 settings_ui，
            // GetFormattedText() 会缓存成原始 key；子菜单展示时 loc 已就绪，这里重新解析修正。
            SetRowLabel(existingTickboxRow, "NOTENOUGHDIFFICULTY-ENABLE_EXTRA_SPEED.title");
        }

        // 4. Slider row 注入（在 tickbox 之后；如果 tickbox 没注入成功则直接在 FastMode 之后）
        var existingSliderRow = content.GetNodeOrNull<Control>(InjectedRowKind.ExtraSpeedMultiplierRowName);
        if (existingSliderRow == null)
        {
            // slider 模板在 SoundSettings 面板，跨面板复制
            var soundPanel = screen.GetNodeOrNull<NSettingsPanel>("%SoundSettings");
            var sliderTemplate = soundPanel?.Content.GetNodeOrNull<Control>(SliderTemplateName);
            if (sliderTemplate == null)
            {
                MainFile.Logger.Warn($"Settings injection: {SliderTemplateName} row not found in SoundSettings");
            }
            else
            {
                // 插入位置：tickbox 行的下一位（如果 tickbox 注入成功）
                var anchorRow = content.GetNodeOrNull<Control>(InjectedRowKind.EnableExtraSpeedRowName)
                                ?? fastModeRow;
                InjectSliderRow(content, sliderTemplate, anchorRow.GetIndex() + 1);
            }
        }
        else
        {
            RefreshSliderRow(existingSliderRow);
            SetRowLabel(existingSliderRow, "NOTENOUGHDIFFICULTY-EXTRA_SPEED_MULTIPLIER.title");
        }
    }

    /// <summary>
    ///     设置注入行的标题文本（解析 mod 的 settings_ui loc key）。
    ///     创建与刷新两条路径都用它：刷新路径重新解析可修正「首次创建时 loc 表未就绪 →
    ///     GetFormattedText() 缓存成原始 key」的问题。
    /// </summary>
    private static void SetRowLabel(Node row, string locKey)
    {
        var label = row.GetNodeOrNull<MegaRichTextLabel>("Label");
        if (label != null)
        {
            label.Text = new LocString("settings_ui", locKey).GetFormattedText();
        }
    }

    // -------------------------------------------------------------------------------------------
    // Tickbox row

    private static void InjectTickboxRow(VBoxContainer container, Control template, int insertIndex)
    {
        // Duplicate(15) = DUPLICATE_GROUPS | DUPLICATE_SIGNALS | DUPLICATE_SCRIPTS | DUPLICATE_USE_INSTANTIATION
        // 完全复制：节点、脚本、signal 连接、group 标签
        var newRow = template.Duplicate(15) as Control;
        if (newRow == null)
        {
            MainFile.Logger.Warn("Tickbox row duplicate failed");
            return;
        }

        newRow.Name = InjectedRowKind.EnableExtraSpeedRowName;
        container.AddChild(newRow);
        container.MoveChild(newRow, insertIndex);

        // 改 label 文本（label 本身没 _Ready 依赖，立即设 OK）
        SetRowLabel(newRow, "NOTENOUGHDIFFICULTY-ENABLE_EXTRA_SPEED.title");

        // 内部 NUploadDataTickbox 的 IsTicked 设置依赖 _Ready 里初始化的 _tickedImage/_notTickedImage
        // 字段——必须等 _Ready 之后再 RefreshTickboxesInRow，否则会 NPE。
        // Godot 4 中 AddChild 触发的 _Ready 是 deferred 到下一帧的，所以用 Ready signal 等待。
        RunAfterReady(newRow, () => RefreshTickboxesInRow(newRow));

        MainFile.Logger.Info($"Settings injection: tickbox row '{newRow.Name}' inserted at index {insertIndex}");
    }

    /// <summary>
    /// 调 row 内所有 NUploadDataTickbox 的 SetFromSettings()。
    /// 配合 InjectedTickboxSetFromSettingsPatch 实现把 UI 显示同步到我们 config 当前值。
    /// </summary>
    private static void RefreshTickboxesInRow(Node row)
    {
        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(row);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n is NUploadDataTickbox tb)
            {
                try
                {
                    tb.SetFromSettings();
                }
                catch (Exception ex)
                {
                    MainFile.Logger.Warn($"SetFromSettings on injected tickbox failed: {ex.Message}");
                }
            }

            foreach (var c in n.GetChildren()) stack.Push(c);
        }
    }

    // -------------------------------------------------------------------------------------------
    // Slider row
    // -------------------------------------------------------------------------------------------

    private static void InjectSliderRow(VBoxContainer container, Control template, int insertIndex)
    {
        var newRow = template.Duplicate(15) as Control;
        if (newRow == null)
        {
            MainFile.Logger.Warn("Slider row duplicate failed");
            return;
        }

        newRow.Name = InjectedRowKind.ExtraSpeedMultiplierRowName;
        container.AddChild(newRow);
        container.MoveChild(newRow, insertIndex);

        // 改 label 文本（立即可改，不依赖 _Ready）
        SetRowLabel(newRow, "NOTENOUGHDIFFICULTY-EXTRA_SPEED_MULTIPLIER.title");

        // ⚠️ 关键：slider 行为必须等 NBgmVolumeSlider._Ready 跑完再绑定！
        //
        // Godot 4 中 AddChild 触发的 _Ready 是 deferred 到下一帧，而我们这里是同步执行：
        //   1. AddChild → 注册 _Ready 到下一帧
        //   2. 立即跑 RefreshSliderRow: disconnect（没东西可断）、设值（没人监听）、改 label
        //   3. 下一帧 _Ready 跑: NSettingsSlider.ConnectSignals 重新 Connect base game handler 并把
        //      label 重置为 "{Value}%"; NBgmVolumeSlider._Ready 末尾还会 SetValueWithoutAnimation
        //      (VolumeBgm * 100) 把 slider 拉到 BgmVolume 对应位置，污染我们的 SpeedMultiplier 字段
        //
        // 修复：用 Ready signal 等 _Ready 跑完之后再 RefreshSliderRow——那时 disconnect 才有意义，
        //       SetValue 才不会被覆盖。
        RunAfterReady(newRow, () => RefreshSliderRow(newRow));

        MainFile.Logger.Info($"Settings injection: slider row '{newRow.Name}' inserted at index {insertIndex}");
    }

    /// <summary>
    /// 在节点 _Ready 跑完后执行 action。如果 _Ready 已经跑过（IsNodeReady=true），立即执行；
    /// 否则注册到 Ready signal。
    ///
    /// 用 Connect 而不是 C# event +=：避免 Godot Source Generator 生成 partial class
    /// dispatcher（这正是早期 SpeedMultiplierController 撞 MonoMod JIT hook 的根因），
    /// 虽然这里我们用的是 base game 的类不会触发，但保持习惯。
    /// </summary>
    private static void RunAfterReady(Node node, Action action)
    {
        if (node.IsNodeReady())
        {
            MainFile.Logger.Info($"[Slider] RunAfterReady: node {node.Name} already ready, running action immediately");
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MainFile.Logger.Error($"RunAfterReady action failed: {ex}");
            }

            return;
        }

        MainFile.Logger.Info($"[Slider] RunAfterReady: node {node.Name} not ready yet, deferring via Ready signal");
        // 用一次性 Callable：执行完调用 disconnect 自己
        Callable callable = default;
        callable = Callable.From(() =>
        {
            MainFile.Logger.Info(
                $"[Slider] RunAfterReady: Ready signal fired for {node.Name}, running deferred action");
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MainFile.Logger.Error($"RunAfterReady deferred action failed: {ex}");
            }
        });
        node.Connect(Node.SignalName.Ready, callable, (uint)GodotObject.ConnectFlags.OneShot);
    }

    /// <summary>
    /// 重置 slider row：把 base game 原 ValueChanged handler（NBgmVolumeSlider.OnValueChanged，
    /// 会写 audio bus）disconnect 掉；范围改成 our config 的 [0.5, 10] 映射；reconnect 我们自己的
    /// handler 写 NotEnoughDifficultyConfig.SpeedMultiplier。
    ///
    /// 用 Meta flag 保证 handler 只绑定一次——即使 NSettingsScreen._Ready 被多次调用。
    /// </summary>
    private static void RefreshSliderRow(Control row)
    {
        MainFile.Logger.Info($"[Slider] RefreshSliderRow called, IsNodeReady={row.IsNodeReady()}");

        // BgmVolume row 实际是个 MarginContainer，内部嵌套结构是：
        //   row(MarginContainer)
        //     ├── Label (MegaRichTextLabel)         <- row 的标题
        //     └── BgmVolumeSlider (NBgmVolumeSlider)
        //         ├── Slider (NSlider)              <- 我们要的 slider
        //         ├── SliderValue (MegaLabel)       <- 我们要的值显示
        //         └── SelectionReticle
        //
        // base game 的 NSettingsSlider 内部 GetNode("Slider") 是相对 NBgmVolumeSlider 自己——
        // 但 row 的根是 MarginContainer，我们直接在 row 上 GetNode("Slider") 找不到。
        // 用按名字递归查找代替路径查找，避免被层级结构 hardcode 锁死。

        var slider = FindFirstChildByName<NSlider>(row, "Slider");
        if (slider == null)
        {
            MainFile.Logger.Warn("[Slider] No NSlider named 'Slider' found in row — abort");
            return;
        }

        MainFile.Logger.Info(
            $"[Slider] Before refresh: Min={slider.MinValue}, Max={slider.MaxValue}, " +
            $"Step={slider.Step}, Value={slider.Value}, HasHookedMeta={slider.HasMeta(SliderHookedMetaKey)}");

        // 第一次进入：disconnect 所有原有 ValueChanged handler + 设范围 + connect 我们的 handler
        if (!slider.HasMeta(SliderHookedMetaKey))
        {
            int disconnected = DisconnectAllValueChangedHandlers(slider);
            MainFile.Logger.Info($"[Slider] Disconnected {disconnected} ValueChanged handlers");

            slider.MinValue = SliderMinInternal;
            slider.MaxValue = SliderMaxInternal;
            slider.Step = SliderStepInternal;
            slider.Connect(Godot.Range.SignalName.ValueChanged,
                Callable.From<double>(OnSpeedSliderValueChanged));
            slider.SetMeta(SliderHookedMetaKey, true);

            MainFile.Logger.Info(
                $"[Slider] After setup: Min={slider.MinValue}, Max={slider.MaxValue}, Step={slider.Step}");
        }

        // 同步当前 config 值到 slider 显示
        var currentInternal = NotEnoughDifficultyConfig.SpeedMultiplier * InternalToDisplay;
        if (currentInternal < SliderMinInternal) currentInternal = SliderMinInternal;
        if (currentInternal > SliderMaxInternal) currentInternal = SliderMaxInternal;

        MainFile.Logger.Info(
            $"[Slider] Setting value to {currentInternal} (config.SpeedMultiplier={NotEnoughDifficultyConfig.SpeedMultiplier})");
        slider.SetValueWithoutAnimation(currentInternal);

        MainFile.Logger.Info($"[Slider] After SetValueWithoutAnimation: slider.Value={slider.Value}");

        UpdateSpeedSliderValueLabel(row);

        // 再次确认状态——可能 SetValueWithoutAnimation 触发了 ValueChanged
        // 我们的 OnSpeedSliderValueChanged 又改了 SpeedMultiplier 又调 UpdateSpeedSliderValueLabel
        MainFile.Logger.Info(
            $"[Slider] Final state: slider.Value={slider.Value}, config.SpeedMultiplier={NotEnoughDifficultyConfig.SpeedMultiplier}");
    }

    /// <summary>
    /// Disconnect 一个 slider 上所有 ValueChanged signal handler。返回 disconnect 数量。
    /// 参考 SteamRandomMatch mod 的做法——遍历 GetSignalConnectionList 拿所有 callable 并断开。
    /// </summary>
    private static int DisconnectAllValueChangedHandlers(NSlider slider)
    {
        int count = 0;
        try
        {
            var connections = slider.GetSignalConnectionList(Godot.Range.SignalName.ValueChanged);
            MainFile.Logger.Info($"[Slider] GetSignalConnectionList returned {connections.Count} entries");

            foreach (Dictionary item in connections)
            {
                // 尝试两种 key 类型：string 和 StringName——Godot 4 中 GetSignalConnectionList
                // 返回的字典 key 类型可能是 StringName 而不是 string，需要兼容
                Variant callableVar = default;
                bool found = false;
                if (item.ContainsKey("callable"))
                {
                    callableVar = item["callable"];
                    found = true;
                }
                else if (item.ContainsKey(new StringName("callable")))
                {
                    callableVar = item[new StringName("callable")];
                    found = true;
                }

                if (!found)
                {
                    MainFile.Logger.Warn(
                        $"[Slider] connection entry without 'callable' key. Keys: " +
                        string.Join(",", item.Keys));
                    continue;
                }

                var callable = callableVar.AsCallable();
                slider.Disconnect(Godot.Range.SignalName.ValueChanged, callable);
                count++;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Slider] DisconnectAllValueChangedHandlers failed: {ex.Message}");
        }

        return count;
    }

    /// <summary>
    /// Slider ValueChanged callback（注入版）。
    /// internal value（5-100）→ SpeedMultiplier（0.5-10）。
    /// 同时更新 row 内的 SliderValue label 显示 "X.Yx"。
    ///
    /// ## 同步约定（v0.4.6 新增）
    /// 写完 SpeedMultiplier 后调 InjectedConfigSyncHelper.NotifyConfigChangedAndPersist()，
    /// 让 BaseLib 配置 UI 同步刷新 + 防抖存盘。详见 InjectedTickboxRouterPatch.cs 顶部注释。
    /// </summary>
    private static void OnSpeedSliderValueChanged(double internalValue)
    {
        try
        {
            var multiplier = internalValue / InternalToDisplay;
            MainFile.Logger.Info(
                $"[Slider] OnSpeedSliderValueChanged(internal={internalValue}) -> SpeedMultiplier={multiplier}");
            NotEnoughDifficultyConfig.SpeedMultiplier = multiplier;

            // 同步约定：写完 config 字段后必须通知 BaseLib + 防抖存盘
            // SaveDebounced 内部 1000ms 防抖——拖动时高频调用会合并成一次实际写盘
            InjectedConfigSyncHelper.NotifyConfigChangedAndPersist();

            // 找当前 row 更新显示——通过查找已注入的 row（slider callback 没法直接拿到 row 引用，
            // 用全局 SceneTree 查 GeneralSettings 面板下的 row）
            UpdateSpeedSliderValueLabelGlobal();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"OnSpeedSliderValueChanged failed: {ex}");
        }
    }

    private static void UpdateSpeedSliderValueLabelGlobal()
    {
        try
        {
            var sceneTree = Engine.GetMainLoop() as SceneTree;
            if (sceneTree == null) return;
            // SettingsScreen 是临时场景，可能没在 active scene tree。直接在 root 下递归找太重——
            // 拿 NSettingsScreen 单例风格的引用？base game 没暴露——只能扫整棵 tree。
            // 简化：依赖 row 仍在 tree 里且 SliderValue 是 row 直接子节点。用 group/find 工具：
            var row = FindNodeByName(sceneTree.Root, InjectedRowKind.ExtraSpeedMultiplierRowName);
            if (row != null) UpdateSpeedSliderValueLabel(row);
        }
        catch
        {
            /* silently ignore — label 失败不影响功能 */
        }
    }

    private static void UpdateSpeedSliderValueLabel(Node row)
    {
        // SliderValue 在 row/BgmVolumeSlider/SliderValue（不是 row 的直接子节点）
        // 用递归按名字查找，不依赖具体层级
        var valLabel = FindFirstChildByName<MegaLabel>(row, "SliderValue");
        if (valLabel != null)
        {
            valLabel.SetTextAutoSize($"{NotEnoughDifficultyConfig.SpeedMultiplier:F1}x");
        }
        else
        {
            MainFile.Logger.Warn("[Slider] UpdateSpeedSliderValueLabel: 'SliderValue' MegaLabel not found");
        }
    }

    /// <summary>简单 BFS 递归找节点。settings 界面较小，性能不是问题。</summary>
    private static Node? FindNodeByName(Node root, string name)
    {
        if (root.Name.ToString() == name) return root;
        foreach (var c in root.GetChildren())
        {
            var found = FindNodeByName(c, name);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// 诊断用：把 node 的整棵子树打到 log，包括类型和路径。
    /// 用于排查"GetNode 找不到 X 节点"——看实际节点叫什么。
    /// </summary>
    private static void DumpNodeTree(Node node, string indent, int depth)
    {
        if (depth > 8) return; // 防御深递归
        try
        {
            MainFile.Logger.Info(
                $"[Slider] {indent}Node: name='{node.Name}', type={node.GetType().Name}");
            foreach (var c in node.GetChildren())
            {
                DumpNodeTree(c, indent + "  ", depth + 1);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Slider] DumpNodeTree failed at depth {depth}: {ex.Message}");
        }
    }

    /// <summary>
    /// 递归查找 root 子树中第一个 T 类型节点。BFS。
    /// </summary>
    private static T? FindFirstChildOfType<T>(Node root) where T : class
    {
        var queue = new System.Collections.Generic.Queue<Node>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            if (n is T t) return t;
            foreach (var c in n.GetChildren()) queue.Enqueue(c);
        }

        return null;
    }

    /// <summary>
    /// 递归按 name 查找 root 子树中第一个名字匹配且类型为 T 的节点。BFS。
    /// 比 GetNode(path) 更 robust：不依赖具体层级结构，节点被嵌套在 wrapper 容器里也能找到。
    /// </summary>
    private static T? FindFirstChildByName<T>(Node root, string name) where T : class
    {
        var queue = new System.Collections.Generic.Queue<Node>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            if (n.Name.ToString() == name && n is T t) return t;
            foreach (var c in n.GetChildren()) queue.Enqueue(c);
        }

        return null;
    }
}

/// <summary>
/// 配合 <see cref="SettingsUiInjectionPatch"/>：在 NSettingsScreen 每次<b>显示</b>时刷新已注入的 row。
///
/// ## 为什么需要这个 patch
///
/// <see cref="SettingsUiInjectionPatch"/> 是 patch <c>NSettingsScreen._Ready</c>——但 _Ready 只在
/// NSettingsScreen 节点首次构造时跑<b>一次</b>。base game 用 NSubmenu 的 Show/Hide 切换显示，
/// 切换时不会重新 _Ready。
///
/// 单纯靠 _Ready 注入会导致：
///   - 首次打开官方设置 → row 注入 → 显示 EnableSpeedMultiplier 当前值（OK）
///   - 在 BaseLib 模组配置 UI 改 EnableSpeedMultiplier 并保存 → property 已变
///   - 切回官方设置 → _Ready <b>不再触发</b> → 我们注入的 row 还显示旧值（bug）
///
/// 反向（官方设置 → 模组配置）天然 OK：BaseLib 的 NConfigTickbox 在 _Ready 时 SetFromProperty，
/// 而 BaseLib 的 NModConfigSubmenu.OnSubmenuShown 每次都重建/重 Load 控件，所以模组配置 UI
/// 总能读到最新 property 值。
///
/// ## 修复
///
/// 监听 <c>NSettingsScreen.OnSubmenuShown</c>（每次显示都调）。复用
/// <see cref="SettingsUiInjectionPatch.InjectRows"/>——它内部判断 row 已存在则
/// 调 RefreshTickboxesInRow / RefreshSliderRow 重新从 property 读值。
///
/// BaseLib 自己也是用这套机制（参见 BaseLib.Patches.Utils.NSettingsScreen_OnSubmenuShown_Patch）。
/// </summary>
[HarmonyPatch(typeof(NSettingsScreen), "OnSubmenuShown")]
internal static class SettingsUiOnSubmenuShownRefreshPatch
{
    [HarmonyPostfix]
    private static void Postfix(NSettingsScreen __instance)
    {
        if (!PatchScope.IsEnabled) return;
        if (__instance == null) return;

        try
        {
            SettingsUiInjectionPatch.InjectRows(__instance);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"SettingsUiOnSubmenuShownRefreshPatch failed: {ex}");
        }
    }
}