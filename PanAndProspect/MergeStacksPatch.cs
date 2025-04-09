using HarmonyLib;
using JetBrains.Annotations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace PanAndProspect;

//[UsedImplicitly]
//[HarmonyPatch(typeof(CollectibleObject))]
public static class MergeStacksPatch
{
    /*
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
        if (sinkAttributes.IsZero()) sinkAttributes = new TreeAttribute(); // TODO not implemented
        if (sourceAttributes.IsZero()) sourceAttributes = new TreeAttribute(); // TODO not implemented
        var mergedAttributes = sourceAttributes.MergeWithPanningAttributes(sinkAttributes, op.MovedQuantity, op.SinkSlot.StackSize);
        ((TreeAttribute)op.SinkSlot.Itemstack.Attributes).SetAttribute(Const.Attr.PanningContents, mergedAttributes);
        return true;
    }
    
    // TODO not implemented
    private static bool IsZero(this TreeAttribute attributes)
    {
        return true;
    }
    
    // TODO not implemented
    private static TreeAttribute MergeWithPanningAttributes(this TreeAttribute source, TreeAttribute sink, int mergedQuantity, int sinkStackSize)
    {
        var mergedAttributes = new TreeAttribute();
        return source;
    }
    */
    public static bool CanContainProspectInfo(this CollectibleObject collObj)
    {
        return collObj is Block block && WildcardUtil.Match("@(game:)(sand|gravel).*", block.Code.ToString());
    }

}