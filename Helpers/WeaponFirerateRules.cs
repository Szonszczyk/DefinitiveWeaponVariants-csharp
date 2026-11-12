namespace DefinitiveWeaponVariants.Helpers;

internal static class WeaponFirerateRules
{
    internal const double MinimumRpm = 1;
    internal const double MaximumRpm = 10000;

    internal static bool IsValid(double rpm) => double.IsFinite(rpm) && rpm >= MinimumRpm && rpm <= MaximumRpm;

    // Firing modes deliberately do not participate: burst and single-fire prefabs
    // may reuse an automatic loop. The client decides which sounds need processing.
    internal static bool HasChanged(double before, double after) => IsValid(before) && IsValid(after) && before != after;
}