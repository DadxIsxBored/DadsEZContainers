using System.Reflection.Emit;

namespace DadsEZContainers.Patches;

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.CanPickup), typeof(bool))]
internal static class ItemPickupDelayPatch
{
    private static double ConfiguredPickupDelay()
    {
        return DadsEZContainersPlugin.PickupDelaySeconds;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo replacement = AccessTools.Method(typeof(ItemPickupDelayPatch), nameof(ConfiguredPickupDelay));
        int replacements = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_R8 &&
                instruction.operand is double value &&
                Math.Abs(value - 0.5d) < double.Epsilon)
            {
                replacements++;
                yield return new CodeInstruction(OpCodes.Call, replacement).MoveLabelsFrom(instruction);
                continue;
            }

            yield return instruction;
        }

        if (replacements != 1)
        {
            DadsEZContainersPlugin.DadsEZContainersLogger.LogError(
                $"Pickup-delay patch expected one delay constant but replaced {replacements}.");
        }
    }
}
