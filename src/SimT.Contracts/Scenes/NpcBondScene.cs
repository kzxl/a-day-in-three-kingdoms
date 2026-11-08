using System.Text.Json.Serialization;
using SimT.Contracts.Playing;
namespace SimT.Contracts.Scenes;
public sealed record BondNodeView(
    [property: JsonPropertyName("npcId")] long NpcId,
    [property: JsonPropertyName("nameVi")] string NameVi,
    [property: JsonPropertyName("facePath")] string FacePath,
    [property: JsonPropertyName("depth")] int Depth);
public sealed record BondEdgeView(
    [property: JsonPropertyName("npcA")] long NpcA,
    [property: JsonPropertyName("npcB")] long NpcB,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("strength")] int Strength);
public sealed record NpcBondScene(
    [property: JsonPropertyName("centerId")] long CenterId,
    [property: JsonPropertyName("nodes")] IReadOnlyList<BondNodeView> Nodes,
    [property: JsonPropertyName("edges")] IReadOnlyList<BondEdgeView> Edges,
    [property: JsonPropertyName("searchResults")] IReadOnlyList<BondNodeView> SearchResults)
{
    public static NpcBondScene Build(INpcBonds bonds, INpcRegistry registry, long centerId, string search = "")
    {
        var center = registry.IsPresent(centerId) ? registry.Find(centerId)! : registry.All.First();
        var depths = new Dictionary<long,int> { [center.Id] = 0 };
        for (int depth = 0; depth < 2; depth++)
        {
            var candidates = depths.Where(p => p.Value == depth).SelectMany(p => bonds.ForNpc(p.Key).Select(b => (Bond:b, Id:b.Other(p.Key))))
                .OrderByDescending(p => p.Bond.Strength).ThenBy(p => p.Id).ToArray();
            foreach (var entry in candidates)
                if (depths.Count < 24 && registry.IsPresent(entry.Id)) depths.TryAdd(entry.Id,depth+1);
        }
        var edges = depths.Keys.SelectMany(bonds.ForNpc).Where(b => depths.ContainsKey(b.NpcA) && depths.ContainsKey(b.NpcB))
            .Distinct().Select(b => new BondEdgeView(b.NpcA,b.NpcB,b.Kind,b.Strength)).ToArray();
        BondNodeView View(NpcDef n, int depth) => new(n.Id,RoleTitleVi.ShortName(n.NameVi),SceneArtCatalog.ResolveNpcArt(n).PortraitFace,depth);
        return new(center.Id, depths.Select(p => View(registry.Find(p.Key)!,p.Value)).ToArray(),edges,
            string.IsNullOrWhiteSpace(search) ? Array.Empty<BondNodeView>() : registry.All.Where(n => n.NameVi.Contains(search,StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(n => n.Id).Take(12).Select(n => View(n,0)).ToArray());
    }
}
