using DefinitiveWeaponVariants.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace TestTrader.Lifecycle;

/// <summary>
/// Replaces expired rotations while allowing SPT to perform its normal trader stock reset.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderCallbacks - 1)]
public sealed class TraderRefreshCoordinator(WeightedAssortService weightedAssortService) : IOnUpdate
{
    public Task<bool> OnUpdateAsync(long secondsSinceLastRun, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        weightedAssortService.RefreshExpiredTraders();
        return Task.FromResult(true);
    }
}
