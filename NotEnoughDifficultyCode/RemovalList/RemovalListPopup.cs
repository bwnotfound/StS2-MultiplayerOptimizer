using System;
using System.Collections.Generic;
using System.Linq;
using BaseLib.Config;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;

namespace NotEnoughDifficulty.NotEnoughDifficultyCode;

/// <summary>
/// 「敌人移除列表」管理弹窗（需求2）。由 NotEnoughDifficultyConfig 的 [ConfigButton] 打开。
///
/// ## 为什么是 static 类 + 直接实例化游戏自带 Godot 控件（而非自定义 Node 派生类）
/// 本 mod 早期踩过坑：自定义 `partial class : Node` 经 Godot 源生成器生成的 InvokeGodotClassMethod
/// 跟 MonoMod(Harmony) 不兼容（速度控制器因此从 Node 改成 static class）。所以这里<b>不定义任何
/// mod 自己的 Node 子类</b>，而是直接 new 游戏自带的 CanvasLayer/ColorRect/OptionButton/Button 等
/// （这些类型的源生成由游戏/引擎完成，安全），逻辑放在 static 方法里，UI 状态用普通对象 Ctx 持有，
/// 信号用 C# lambda 接。
///
/// ## 为什么用标准 Godot 控件（而非游戏/BaseLib UI 组件）
/// - BaseLib NConfigDropdown 只支持 enum，承载不了运行时扫描的动态怪物列表。
/// - 游戏原生 NSettingsDropdown 是 abstract、强依赖内部节点结构，复用脆弱难测。
/// - 标准控件（OptionButton 原生支持运行时 AddItem、CanvasLayer 全屏遮罩做模态）最鲁棒。
///   样式较朴素（默认主题），功能完整优先，后续可美化。
///
/// ## 结构
/// CanvasLayer(顶层) → 半透明 ColorRect(挡背后输入) → CenterContainer → PanelContainer
///   → 标题 / 3 个 tier 下拉行(普通/精英/boss + 各「添加」) / 当前列表(滚动，每行「移除」) / 关闭
///
/// ## 兜底
/// 增删走 ExtraActsConfig.Add/RemoveExclusion（重复增、不存在删都安全）；增删后 Cfg.Save() 落盘，
/// 下次 act4/5 抽取生效。残留的「已不存在 id」显示为纯 id，仍可移除。
/// </summary>
public static class RemovalListPopup
{
    private const string LocPrefix = "NOTENOUGHDIFFICULTY-";

    private sealed class Ctx
    {
        public ModConfig? Cfg;
        public CanvasLayer Layer = null!;
        public VBoxContainer ListVbox = null!;
        public readonly Dictionary<string, string> IdToName = new(StringComparer.Ordinal);
    }

    /// <summary>打开弹窗。anchor：场景树里任意节点（用来拿 SceneTree.Root）。cfg：增删后落盘用。</summary>
    public static void Open(Node? anchor, ModConfig? cfg)
    {
        try
        {
            var root = anchor?.GetTree()?.Root;
            if (root == null)
            {
                MainFile.Logger.Error("RemovalListPopup: cannot resolve scene root; abort open");
                return;
            }

            var ctx = new Ctx { Cfg = cfg, Layer = new CanvasLayer { Layer = 128 } };
            root.AddChild(ctx.Layer);
            Build(ctx);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"RemovalListPopup.Open failed: {ex}");
        }
    }

    private static void Build(Ctx ctx)
    {
        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.6f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ctx.Layer.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 600) };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        margin.AddChild(col);

        col.AddChild(new Label { Text = Loc("REMOVAL_LIST"), HorizontalAlignment = HorizontalAlignment.Center });

        col.AddChild(MakeTierRow(ctx, RoomType.Monster, Loc("TIER_MONSTER")));
        col.AddChild(MakeTierRow(ctx, RoomType.Elite, Loc("TIER_ELITE")));
        col.AddChild(MakeTierRow(ctx, RoomType.Boss, Loc("TIER_BOSS")));

        col.AddChild(new HSeparator());
        col.AddChild(new Label { Text = Loc("CURRENT_LIST") });

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        col.AddChild(scroll);

        ctx.ListVbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(ctx.ListVbox);

        var close = new Button { Text = Loc("CLOSE") };
        close.Pressed += () =>
        {
            Persist(ctx);
            ctx.Layer.QueueFree();
        };
        col.AddChild(close);

        RefreshList(ctx);
    }

    private static Control MakeTierRow(Ctx ctx, RoomType tier, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(96, 0) });

        var dropdown = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        IReadOnlyList<(string id, string name)> items;
        try
        {
            items = ExtraActsConfig.ListEncounters(tier);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"ListEncounters({tier}) failed: {ex}");
            items = new List<(string, string)>();
        }

        foreach (var (id, name) in items)
        {
            int idx = dropdown.ItemCount;
            dropdown.AddItem(name);
            dropdown.SetItemMetadata(idx, id);
            ctx.IdToName[id] = name; // 顺便建 id->name 映射，供当前列表显示
        }

        row.AddChild(dropdown);

        var addBtn = new Button { Text = Loc("ADD") };
        addBtn.Pressed += () =>
        {
            if (dropdown.Selected < 0) return;
            var id = dropdown.GetItemMetadata(dropdown.Selected).AsString();
            if (ExtraActsConfig.AddExclusion(id))
            {
                Persist(ctx);
                RefreshList(ctx);
            }
        };
        row.AddChild(addBtn);

        return row;
    }

    private static void RefreshList(Ctx ctx)
    {
        foreach (var child in ctx.ListVbox.GetChildren())
        {
            ctx.ListVbox.RemoveChild(child);
            child.QueueFree();
        }

        List<string> ids;
        try
        {
            ids = ExtraActsConfig.GetExcludedIds().OrderBy(x => x, StringComparer.Ordinal).ToList();
        }
        catch
        {
            ids = new List<string>();
        }

        if (ids.Count == 0)
        {
            ctx.ListVbox.AddChild(new Label { Text = Loc("LIST_EMPTY") });
            return;
        }

        foreach (var id in ids)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var display = ctx.IdToName.TryGetValue(id, out var n) ? $"{n}  ({id})" : id;
            row.AddChild(new Label
            {
                Text = display,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            });

            var capturedId = id;
            var rm = new Button { Text = Loc("REMOVE") };
            rm.Pressed += () =>
            {
                if (ExtraActsConfig.RemoveExclusion(capturedId))
                {
                    Persist(ctx);
                    RefreshList(ctx);
                }
            };
            row.AddChild(rm);

            ctx.ListVbox.AddChild(row);
        }
    }

    private static void Persist(Ctx ctx)
    {
        try
        {
            ctx.Cfg?.Save();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"RemovalListPopup persist failed: {ex}");
        }
    }

    private static string Loc(string key)
    {
        try
        {
            var s = LocString.GetIfExists("settings_ui", $"{LocPrefix}{key}.title");
            var text = s?.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        catch
        {
            // ignore，回退 key
        }

        return key;
    }
}