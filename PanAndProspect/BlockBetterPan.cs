using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PanAndProspect;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class BlockBetterPan : BlockPan
{
    [CanBeNull]
    private ILoadedSound Sound
    {
        get => this.GetBaseField<ILoadedSound>("sound");
        set => this.SetBaseField("sound", value);
    }

    [CanBeNull]
    private Dictionary<string, PanningDrop[]> DropsBySourceMat => this.GetBaseField<Dictionary<string, PanningDrop[]>>("dropsBySourceMat");

    private ItemStack Resolve(EnumItemClass type, string code)
    {
        if (type == EnumItemClass.Block)
        {
            var block = api.World.GetBlock(new AssetLocation(code));
            if (block != null) return new ItemStack(block);
            api.World.Logger.Error("Failed resolving panning block drop with code {0}. Will skip.", code);
            return null;
        }
        var item = api.World.GetItem(new AssetLocation(code));
        if (item != null) return new ItemStack(item);
        api.World.Logger.Error("Failed resolving panning item drop with code {0}. Will skip.", code);
        return null;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
        if (!firstEvent)
            return;
        var byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null) return;
        if (blockSel != null &&
            !byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            return;
        var blockMatCode = GetBlockMaterialCode(slot.Itemstack);
        if (!byEntity.FeetInLiquid && api is ICoreClientAPI capi && blockMatCode != null)
        {
            capi.TriggerIngameError(this, "notinwater", Lang.Get("ingameerror-panning-notinwater"));
            return;
        }
        if (blockMatCode == null && blockSel != null)
        {
            TryTakeMaterial(slot, byEntity, blockSel.Position);
            slot.Itemstack.TempAttributes.SetBool("canpan", false);
            return;
        }
        if (blockMatCode != null)
            slot.Itemstack.TempAttributes.SetBool("canpan", true);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        Sound?.Stop();
        Sound = null;
        if (!(secondsUsed >= 3.4f)) return;
        var code = GetBlockMaterialCode(slot.Itemstack);
        if (api is ICoreServerAPI sapi && code != null)
        {
            var prospects = StoreProspectsBehavior.GetProspectsFromPos(sapi, blockSel.Position);
            CreateDrop(byEntity, code, prospects);
        }
        RemoveMaterial(slot);
        slot.MarkDirty();
        byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(4f);
    }

    private void CreateDrop(EntityAgent byEntity, string fromBlockCode, [CanBeNull] Dictionary<string, double> prospects)
    {
        var player = (byEntity as EntityPlayer)?.Player;
        PanningDrop[] drops = null;
        if (DropsBySourceMat == null)
            throw new InvalidOperationException("Coding error, no drops defined for source mat " + fromBlockCode);
        foreach (var val in DropsBySourceMat.Keys.Where(val => WildcardUtil.Match(val, fromBlockCode)))
        {
            drops = DropsBySourceMat[val];
        }
        if (drops == null)
            throw new InvalidOperationException("Coding error, no drops defined for source mat " + fromBlockCode);
        var rocktype = api.World.GetBlock(new AssetLocation(fromBlockCode))?.Variant["rock"];
        drops.Shuffle(api.World.Rand);
        foreach (var drop in drops)
        {
            var rnd = api.World.Rand.NextDouble();
            var extraMul = 1f;
            if (drop.DropModbyStat != null)
            {
                // If the stat does not exist, then GetBlended returns 1 \o/
                extraMul = byEntity.Stats.GetBlended(drop.DropModbyStat);
            }
            if (WildcardUtil.Match("@(ore|gem|nugget)-.*", drop.Code.Path))
            {
                PanAndProspectCore.Logger.Warning("Prospecting drop {0} with ore name {1}", drop.Code.Path, drop.Code.EndVariant());
                var oreName = drop.Code.EndVariant();
                var quantity = 0d;
                prospects?.TryGetValue(oreName, out quantity);
                PanAndProspectCore.Logger.Warning("Adding modifier {0} for ore name {1}", quantity, oreName);
                drop.Chance.avg = (float) quantity/100;
            }
            var val = drop.Chance.nextFloat() * extraMul;
            var stack = drop.ResolvedItemstack;
            if (drop.Code.Path.Contains("{rocktype}"))
                stack = Resolve(drop.Type, drop.Code.Path.Replace("{rocktype}", rocktype));
            if (!(rnd < val) || stack == null) continue;
            stack = stack.Clone();
            if (player == null || !player.InventoryManager.TryGiveItemstack(stack, true))
                api.World.SpawnItemEntity(stack, byEntity.ServerPos.XYZ);
            break;
        }
    }
    
    private static void SetMaterial(ItemSlot slot, Block block, [CanBeNull] Dictionary<string, double> prospects)
    {
        slot.Itemstack.Attributes.SetString("materialBlockCode", block.Code.ToShortString());
        PanAndProspectCore.Logger.Warning("Set prospects for {0} to {1}", block.Code.ToShortString(), string.Join(",", prospects?.Keys ?? Enumerable.Empty<string>()));
        if (prospects == null) return;
        slot.Itemstack.Attributes[Const.Attr.PanningContents] = prospects.ToTreeAttribute();
    }

    private new static void RemoveMaterial(ItemSlot slot)
    {
        slot.Itemstack.Attributes.RemoveAttribute("materialBlockCode");
        slot.Itemstack.Attributes.RemoveAttribute(Const.Attr.PanningContents);
    }
    
    protected override void TryTakeMaterial(ItemSlot slot, EntityAgent byEntity, BlockPos position)
    {
        var block = api.World.BlockAccessor.GetBlock(position);
        if (!IsPannableMaterial(block)) return;
        if (api.World.BlockAccessor.GetBlock(position.UpCopy()).Id != 0)
        {
            if (api is ICoreClientAPI capi)
                capi.TriggerIngameError(this, "noair", Lang.Get("ingameerror-panning-requireairabove"));
            return;
        }
        if (api is not ICoreServerAPI sapi) return;
        Dictionary<string, double> prospects = null;
        if (block.GetBehavior<StoreProspectsBehavior>() is { } storeProspectsBehavior)
            prospects = storeProspectsBehavior.GetOrGenProspectsFromPos(sapi, position);
        var layer = block.Variant["layer"];
        if (layer != null)
        {
            var baseCode = block.FirstCodePart() + "-" + block.FirstCodePart(1);
            var origblock = api.World.GetBlock(new AssetLocation(baseCode));
            SetMaterial(slot, origblock, prospects);
            if (layer == "1")
            {
                api.World.BlockAccessor.SetBlock(0, position);
            } else
            {
                var code = block.CodeWithVariant("layer", "" + (int.Parse(layer) - 1));
                var reducedBlock = api.World.GetBlock(code);
                api.World.BlockAccessor.SetBlock(reducedBlock.BlockId, position);
            }
            api.World.BlockAccessor.TriggerNeighbourBlockUpdate(position);
        }
        else
        {
            var pannedBlock = block.Attributes["pannedBlock"].AsString();
            var reducedBlock = api.World.GetBlock(pannedBlock != null ?
                AssetLocation.Create(pannedBlock, block.Code.Domain) :
                block.CodeWithVariant("layer", "7"));
            if (reducedBlock != null)
            {
                SetMaterial(slot, block, prospects);
                api.World.BlockAccessor.SetBlock(reducedBlock.BlockId, position);
                api.World.BlockAccessor.TriggerNeighbourBlockUpdate(position);
            }
            else
                PanAndProspectCore.Logger.Warning("Missing \"pannedBlock\" attribute for pannable block " + block.Code.ToShortString());
        }
        slot.MarkDirty();
    }
}