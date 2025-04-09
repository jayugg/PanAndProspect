using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProtoBuf;
using Vintagestory.API.Server;

namespace PanAndProspect;

[ProtoContract]
public class ProspectingData
{
    private readonly ConditionalWeakTable<IServerChunk, ProspectingData> _prospectingDataCache = new();
    
    [ProtoMember(1)] public Dictionary<string, float> Data = new();
}