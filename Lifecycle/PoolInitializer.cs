using DefinitiveWeaponVariants.Services;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace TestTrader.Lifecycle;

/// <summary>
/// Seals the contributed source pool after trader setup and before initial flea offers are generated.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.RagfairCallbacks - 1)]
public sealed class PoolInitializer(WeightedAssortService weightedAssortService, ModDatabaseLoader modDatabaseLoader) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        weightedAssortService.InitializeConfiguredTraders(modDatabaseLoader.TraderBase.Id);
        return Task.CompletedTask;
    }
}
