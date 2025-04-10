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

    public override void OnLoaded(ICoreAPI api)
    {
        this.api = api;
        if (api.Side != EnumAppSide.Server)
            return;
        PanAndProspectCore.Logger.Warning("Propick workspace loaded: {0}", _proPickWorkSpace == null ? "null" : _proPickWorkSpace.ToString());
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack,
        ref EnumHandling handling)
    {
        PanAndProspectCore.Logger.Warning("StoreProspectsBehavior.DoPlaceBlock: {0}", blockSel.Position);
        world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position, byItemStack);
        var chunk = (world.Api as ICoreServerAPI)?.WorldManager.GetChunk(blockSel.Position);
        var blockEntityGeneric = new BlockEntityGeneric
        {
            Pos = blockSel.Position
        };
        blockEntityGeneric.AddBehavior<BlockEntityBehaviorStoreProspects>();
        if (chunk != null)
            chunk.BlockEntities[blockSel.Position] = blockEntityGeneric;
        blockEntityGeneric.Initialize(world.Api);
        //if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not { } blockEntity ||
        //    blockEntity.GetBehavior<BlockEntityBehaviorStoreProspects>() is not { } beBehavior) return true;
        var beBehavior = blockEntityGeneric.GetBehavior<BlockEntityBehaviorStoreProspects>();
        if (byItemStack.Attributes[Const.Attr.PanningContents] is not ITreeAttribute prospects) return true;
        var treeAttributes = new TreeAttribute();
        treeAttributes.SetAttribute(Const.Attr.PanningContents, prospects);
        beBehavior.FromTreeAttributes(treeAttributes, world);
        blockEntityGeneric.MarkDirty();
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
        handling = EnumHandling.PreventSubsequent;
        Dictionary<string, double> prospects;
        if (world.BlockAccessor.GetBlockEntity(pos) is { } blockEntity
            && blockEntity.GetBehavior<BlockEntityBehaviorStoreProspects>() is { } beBehavior)
            prospects = beBehavior.Prospects;
        else
        {
            var proPickReading = GenProbeResults(world, pos);
            PanAndProspectCore.Logger.Warning("Prospects: {0}", proPickReading.OreReadings.Count);
            prospects = proPickReading.OreReadings
                .Where(kvp => kvp.Value.PartsPerThousand > PropickReading.MentionThreshold)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.PartsPerThousand);
        }
        var treeAttributes = new TreeAttribute();
        treeAttributes.SetAttribute(Const.Attr.PanningContents, prospects.ToTreeAttribute());
        var dropStack = new ItemStack(world.BlockAccessor.GetBlock(pos)) { Attributes = treeAttributes };
        return [dropStack];
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