using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using ProtoBuf;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace PanAndProspect;

public partial class PanAndProspectCore
{
    private readonly ConditionalWeakTable<IServerChunk, ProspectingData> _chunkProspectingDataCache = new();
    
    public void AddProspectsAtPosition(ICoreServerAPI api, BlockPos pos, Dictionary<string, double> prospects)
    {
        var prospectingData = GetOrCreateProspectingData(api, pos);
        prospectingData.SetProspects(pos, prospects);
    }
    
    public ProspectingData GetOrCreateProspectingData(ICoreServerAPI api, BlockPos pos)
    {
        var chunk = api.WorldManager.GetChunk(pos);
        return chunk == null ? null : GetOrCreateProspectingData(chunk);
    }
    
    public Dictionary<string, double> GetProspectsFromPos(ICoreServerAPI api, BlockPos pos)
    {
        var chunk = api.WorldManager.GetChunk(pos);
        var data = chunk == null ? null : GetOrCreateProspectingData(chunk, false);
        return data?.GetProspects(pos);
    }
    
    /// <summary>
    /// Retrieves or creates prospecting data for the given chunk.
    /// </summary>
    /// <param name="chunk">The server chunk to retrieve or create data for.</param>
    /// <param name="create">Whether to create new data if none exists.</param>
    /// <returns>The prospecting data for the chunk, or null if not found and creation is not allowed.</returns>
    private ProspectingData GetOrCreateProspectingData(IServerChunk chunk, bool create = true)
    {
        if (_chunkProspectingDataCache.TryGetValue(chunk, out var cachedData))
            return cachedData;
        var serializedData = chunk.GetServerModdata(Const.ModDataKey);
        var prospectingData = serializedData != null 
            ? SerializerUtil.Deserialize<ProspectingData>(serializedData) 
            : create ? new ProspectingData() : null;
        if (prospectingData == null) return null;
        _chunkProspectingDataCache.Add(chunk, prospectingData);
        if (chunk.LiveModData.TryGetValue(Const.ModDataKey, out var serializerObj) && serializerObj is SerializationCallback serializer)
            serializer.OnSerialization += c => c.SetServerModdata(Const.ModDataKey, SerializerUtil.Serialize(prospectingData));
        else
            chunk.LiveModData[Const.ModDataKey] = new SerializationCallback(chunk)
            {
                OnSerialization = c => c.SetServerModdata(Const.ModDataKey, SerializerUtil.Serialize(prospectingData))
            };
        return prospectingData;
    }
    
    [ProtoContract]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class SerializationCallback(IServerChunk chunk)
    {
        public delegate void OnSerializationDelegate(IServerChunk chunk);

        public OnSerializationDelegate OnSerialization;

        [ProtoBeforeSerialization]
        private void BeforeSerialization()
        {
            OnSerialization(chunk);
        }
    }
}