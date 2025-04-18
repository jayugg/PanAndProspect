using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;
using Core = Vintagestory.GameContent.Core;

namespace PanAndProspect;

[UsedImplicitly]
public class StoreProspectsBehavior(Block block) : BlockBehavior(block)
{
    private ICoreAPI api;
    private ProPickWorkSpace _proPickWorkSpace => ObjectCacheUtil.GetOrCreate(api, "propickworkspace", () =>
    {
        var proPickWorkSpace = new ProPickWorkSpace();
        proPickWorkSpace.OnLoaded(api);
        return proPickWorkSpace;
    });

    public override void OnLoaded(ICoreAPI coreApi)
    {
        api = coreApi;
        if (coreApi.Side != EnumAppSide.Server)
            return;
        PanAndProspectCore.Logger.Warning("Propick workspace loaded: {0}", _proPickWorkSpace == null ? "null" : _proPickWorkSpace.ToString());
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack,
        ref EnumHandling handling)
    {
        world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position, byItemStack);
        if (byItemStack.Attributes[Const.Attr.PanningContents] is not ITreeAttribute prospects) return true;
        var prospectsDict = prospects.ToDictionary(kvp => kvp.Key, kvp => ((DoubleAttribute)kvp.Value).value);
        if (world.Api is ICoreServerAPI sapi)
            PanAndProspectCore.GetInstance(world.Api).AddProspectsAtPosition(sapi, blockSel.Position, prospectsDict);
        handling = EnumHandling.PreventSubsequent;
        return true;
    }

    public override ItemStack[] GetDrops(
        IWorldAccessor world,
        BlockPos pos,
        IPlayer byPlayer,
        ref float dropChanceMultiplier,
        ref EnumHandling handling)
    {
        if (byPlayer is { WorldData.CurrentGameMode: EnumGameMode.Creative } ||
            !world.BlockAccessor.GetBlock(pos).CanContainProspectInfo())
            return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
        if (world.Api is not ICoreServerAPI sapi) return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
        handling = EnumHandling.PreventSubsequent;
        var prospects = GetProspectsFromPos(sapi, pos);
        if (prospects != null)
            ClearProspectsAtPos(sapi, pos);
        else 
            prospects = GenProspectsAtPos(world, pos);
        var treeAttributes = new TreeAttribute();
        treeAttributes.SetAttribute(Const.Attr.PanningContents, prospects.ToTreeAttribute());
        var dropStack = new ItemStack(world.BlockAccessor.GetBlock(pos)) { Attributes = treeAttributes };
        return [dropStack];
    }

    public Dictionary<string, double> GenProspectsAtPos(IWorldAccessor world, BlockPos pos)
    {
        PanAndProspectCore.Logger.Warning("GenProspectsAtPos: {0}", pos);
        var proPickReading = GenProbeResults(world, pos);
        PanAndProspectCore.Logger.Warning("Probe results: {0}", proPickReading.OreReadings.Count);
        var prospects = proPickReading.OreReadings
            .Where(kvp => kvp.Value.PartsPerThousand > PropickReading.MentionThreshold)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.PartsPerThousand);
        PanAndProspectCore.Logger.Warning("Prospects: {0}", prospects.Count);
        return prospects;
    }

    [CanBeNull]
    public static Dictionary<string, double> GetProspectsFromPos( ICoreServerAPI sapi, BlockPos pos)
    {
        return PanAndProspectCore.GetInstance(sapi).GetProspectsFromPos(sapi, pos);
    }
    
    [CanBeNull]
    public Dictionary<string, double> GetOrGenProspectsFromPos(ICoreServerAPI sapi, BlockPos pos)
    {
        PanAndProspectCore.Logger.Warning("GetOrGenProspectsFromPos: {0}", pos);
        var prospects = PanAndProspectCore.GetInstance(sapi).GetProspectsFromPos(sapi, pos);
        if (prospects != null) return prospects;
        prospects = GenProspectsAtPos(sapi.World, pos);
        PanAndProspectCore.GetInstance(sapi).AddProspectsAtPosition(sapi, pos, prospects);
        return prospects;
    }
    
    public static void ClearProspectsAtPos( ICoreServerAPI sapi, BlockPos pos)
    {
        PanAndProspectCore.GetInstance(sapi).ClearProspectsAtPos(sapi, pos);
    }

    private PropickReading GenProbeResults(IWorldAccessor world, BlockPos pos)
    {
        var api = world.Api;
        if (api.ModLoader.GetModSystem<GenDeposits>()?.Deposits == null)
            return null;
        var regionSize = world.BlockAccessor.RegionSize;
        var mapRegion = world.BlockAccessor.GetMapRegion(pos.X / regionSize, pos.Z / regionSize);
        var num1 = pos.X % regionSize;
        var num2 = pos.Z % regionSize;
        pos = pos.Copy();
        pos.Y = world.BlockAccessor.GetTerrainMapheightAt(pos);
        var rockColumn = _proPickWorkSpace.GetRockColumn(pos.X, pos.Z);
        var propickReading = new PropickReading
        {
            Position = new Vec3d(pos.X, pos.Y, pos.Z)
        };
        foreach (var (oreCode, oreMap) in mapRegion.OreMaps)
        {
            var innerSize = oreMap.InnerSize;
            var unpaddedColorLerped = oreMap.GetUnpaddedColorLerped(num1 / (float) regionSize * innerSize, num2 / (float) regionSize * innerSize);
            if (!_proPickWorkSpace.depositsByCode.TryGetValue(oreCode, out var deposit)) continue;
            deposit.GetPropickReading(pos, unpaddedColorLerped, rockColumn, out var ppt, out var totalFactor);
            if (totalFactor > 0.0)
                propickReading.OreReadings[oreCode] = new OreReading()
                {
                    TotalFactor = totalFactor,
                    PartsPerThousand = ppt
                };
        }
        return propickReading;
    }
}