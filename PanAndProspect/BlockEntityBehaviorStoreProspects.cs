using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PanAndProspect;

[UsedImplicitly]
public class BlockEntityBehaviorStoreProspects(BlockEntity blockentity) : BlockEntityBehavior(blockentity)
{
    public Dictionary<string, double> Prospects { get; set; } = new();

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        var prospects = tree.GetTreeAttribute(Const.Attr.PanningContents);
        var prospectsDict = prospects.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value as DoubleAttribute)?.value ?? 0.0
        );
        prospectsDict = prospectsDict.Where(kvp => kvp.Value > PropickReading.MentionThreshold) as Dictionary<string, double>;
        Prospects = prospectsDict;
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