using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace MultiplayerOptimizer.MultiplayerOptimizerCode;

[HarmonyPatch(typeof(Ironclad), nameof(Ironclad.StartingDeck), MethodType.Getter)]
public static class IroncladStartingDeckPatch
{
    public static void Postfix(ref IEnumerable<CardModel> __result)
    {
        CardModel testCard = ModelDb.Card<TestCard>();

        __result = __result.Concat([testCard]);

        Godot.GD.Print("[MultiplayerOptimizer] Patched Ironclad.StartingDeck: added TestCard.");
    }
}