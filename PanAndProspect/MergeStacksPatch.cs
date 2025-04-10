using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace PanAndProspect;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[HarmonyPatch(typeof(CollectibleObject))]
public static class MergeStacksPatch
{
    
    [HarmonyPrefix, HarmonyPatch(nameof(CollectibleObject.TryMergeStacks))]
    public static bool TryMergeStacksPrefix(CollectibleObject __instance, ItemStackMergeOperation op)
    {
        if (!op.SinkSlot.Itemstack.Collectible.CanContainProspectInfo()) return true;
        op.MovableQuantity = __instance.GetMergableQuantity(op.SinkSlot.Itemstack, op.SourceSlot.Itemstack, op.CurrentPriority);
        if (op.MovableQuantity == 0) return false;
        if (!op.SinkSlot.CanTakeFrom(op.SourceSlot, op.CurrentPriority)) return false;
        op.MovedQuantity = GameMath.Min(op.SinkSlot.GetRemainingSlotSpace(op.SourceSlot.Itemstack), op.MovableQuantity, op.RequestedQuantity);
        var sourceAttributes = op.SourceSlot.Itemstack.Attributes[Const.Attr.PanningContents] as TreeAttribute;
        var sinkAttributes = op.SinkSlot.Itemstack.Attributes[Const.Attr.PanningContents] as TreeAttribute;
        sinkAttributes ??= new TreeAttribute();
        sourceAttributes ??= new TreeAttribute();
        var mergedAttributes = sourceAttributes.MergePanningAttributes(sinkAttributes, op.MovedQuantity, op.SinkSlot.StackSize);
        ((TreeAttribute)op.SinkSlot.Itemstack.Attributes).SetAttribute(Const.Attr.PanningContents, mergedAttributes);
        return true;
    }
    
    private static TreeAttribute MergePanningAttributes(this TreeAttribute source, TreeAttribute sink, int movedQuantity, int sinkStackSize)
    {
        var mergedAttributes = new TreeAttribute();
        foreach (var key in source.Keys.Concat(sink.Keys))
        {
            var sourceValue = source.GetDouble(key, 0);
            var sinkValue = sink.GetDouble(key, 0);
            var mergedValue = (sourceValue * movedQuantity + sinkValue * sinkStackSize) / (movedQuantity + sinkStackSize);
            mergedAttributes.SetDouble(key, mergedValue);
        }
        return mergedAttributes;
    }
}