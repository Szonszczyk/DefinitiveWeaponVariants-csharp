using System.Collections.Frozen;

namespace DefinitiveWeaponVariants.Integrations;

/// <summary>
/// Optional reflection entry point for Amonya. Uses only framework types so
/// Amonya can coordinate updates without referencing the DWV assembly.
/// </summary>
public static class AmonyaIntegration
{
    private static APBSIntegration? _integration;
    public static IReadOnlySet<string> WeaponVariantIds { get; internal set; } = FrozenSet<string>.Empty;
    internal static void Initialize(APBSIntegration integration) => Volatile.Write(ref _integration, integration);

    /// <summary>
    /// Run a complete blacklist transaction under DWV's APBS gate after pending
    /// weapon recovery. The callback must await its refresh and any rollback.
    /// Call after DWV's PostLoad initialization and before modifying the file.
    /// </summary>
    public static Task RunBlacklistUpdateAsync(string blacklistPath,
        Func<CancellationToken, Task> update, CancellationToken cancellationToken)
    {
        var integration = Volatile.Read(ref _integration)
            ?? throw new InvalidOperationException("DWV APBS coordination has not initialized yet.");
        return integration.RunExternalBlacklistUpdateAsync(blacklistPath, update, cancellationToken);
    }
}
