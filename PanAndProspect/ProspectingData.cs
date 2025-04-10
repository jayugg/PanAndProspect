using System.Collections.Generic;
using JetBrains.Annotations;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace PanAndProspect;

[ProtoContract]
public class ProspectingData
{
    [ProtoMember(1)]
    private readonly Dictionary<BlockPos, Dictionary<string, double>> _data = new();
    
    [CanBeNull]
    public Dictionary<string, double> GetProspects(BlockPos pos)
    {
        _data.TryGetValue(pos, out var prospects);
        return prospects;
    }
    
    public void SetProspects(BlockPos pos, Dictionary<string, double> prospects)
    {
        if (prospects == null || prospects.Count == 0)
            _data.Remove(pos);
        else
            _data[pos] = prospects;
    }
}