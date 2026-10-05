using DefinitiveWeaponVariants.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace TestTrader.Lifecycle;

/// <summary>
/// Seals the contributed source pool after trader setup and before initial flea offers are generated.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.RagfairCallbacks - 1)]
public sealed class PoolInitializer(WeightedAssortService weightedAssortService) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        weightedAssortService.InitializeConfiguredTraders();
        return Task.CompletedTask;
    }
}
