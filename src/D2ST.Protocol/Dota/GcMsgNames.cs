using System.Collections.Frozen;

namespace D2ST.Protocol.Dota;

/// <summary>
/// Resolves a GC message id to the generated enum name(s) that carry it, so a
/// diagnostics dump is readable without looking the number up by hand. Ids are
/// only unique within an enum (28 is both an SO message and a Dota message), so
/// every candidate name is reported.
/// </summary>
public static class GcMsgNames
{
    private static readonly FrozenDictionary<uint, string> Names = Build();

    /// <summary>The enum name(s) for a message id, or "unknown" when nothing matches.</summary>
    public static string Describe(uint messageType) =>
        Names.TryGetValue(messageType, out var name) ? name : "unknown";

    private static FrozenDictionary<uint, string> Build()
    {
        var byId = new Dictionary<uint, List<string>>();

        foreach (var type in new[] { typeof(EGCBaseMsg), typeof(EGCBaseClientMsg), typeof(ESOMsg), typeof(EGCItemMsg), typeof(EGCEconBaseMsg), typeof(EDOTAGCMsg) })
        {
            foreach (var value in Enum.GetValues(type))
            {
                var id = Convert.ToInt64(value);
                if (id is < 0 or > uint.MaxValue)
                {
                    continue;
                }

                var names = byId.TryGetValue((uint)id, out var existing) ? existing : byId[(uint)id] = [];
                var name = $"{type.Name}.{Enum.GetName(type, value)}";
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }
        }

        return byId.ToFrozenDictionary(entry => entry.Key, entry => string.Join(" | ", entry.Value));
    }
}
