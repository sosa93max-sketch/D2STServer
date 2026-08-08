namespace D2ST.GameCoordinator.Econ;

public static class EconItemIdentity
{
    public static ulong ItemId(uint accountId, uint defIndex) => ((ulong)accountId << 32) | defIndex;
}
