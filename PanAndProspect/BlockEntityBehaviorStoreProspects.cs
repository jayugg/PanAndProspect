using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PanAndProspect;

[UsedImplicitly]
public class BlockEntityBehaviorStoreProspects(BlockEntity blockEntity) : BlockEntityBehavior(blockEntity)
{
    public Dictionary<string, double> Prospects { get; set; } = new();

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        var prospects = tree.GetTreeAttribute(Const.Attr.PanningContents);
        if (prospects is TreeAttribute treeAttribute)
        {
            Prospects = treeAttribute
                .Where(kvp => kvp.Value is DoubleAttribute da && da.value > PropickReading.MentionThreshold)
                .ToDictionary(kvp => kvp.Key, kvp => ((DoubleAttribute)kvp.Value).value);
        }
        else
        {
            Prospects = new Dictionary<string, double>();
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        var prospects = new TreeAttribute();
        foreach (var kvp in Prospects)
        {
            prospects.SetDouble(kvp.Key, kvp.Value);
        }
        ((TreeAttribute)tree).SetAttribute(Const.Attr.PanningContents, prospects);
    }
}