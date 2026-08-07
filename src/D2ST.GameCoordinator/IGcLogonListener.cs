namespace D2ST.GameCoordinator;

/// <summary>
/// Notified when a client reaches the GC (its ClientHello is answered). It is
/// the hook for state the client is given without asking for it and that is not
/// a Shared Object — the default chat channels it is put in — because
/// <see cref="IGcWelcomeContributor"/> can only add caches to the welcome.
/// </summary>
public interface IGcLogonListener
{
    void OnLogon(GcContext context);
}
