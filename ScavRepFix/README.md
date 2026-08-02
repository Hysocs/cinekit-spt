# ScavRepFix

ScavRepFix is a small BepInEx client plugin for SPT 4.1.0. It prevents the
known null-reference exception thrown by
`SPT.SinglePlayer.Patches.ScavMode.ScavRepAdjustmentPatch.PatchPrefix` from
flooding the EFT error log.

The guard is intentionally narrow: it suppresses only
`NullReferenceException` instances whose stack trace identifies that exact SPT
patch. Other exceptions from `BaseStatisticsManager.OnEnemyKill` are preserved.

When the SPT prefix fails, its scav-reputation adjustment for that kill may not
be applied. Normal EFT kill processing can continue without the exception being
propagated.

## Build

```powershell
dotnet build ScavRepFix.sln -c Release -p:SkipDeploy=true
```

The release build creates `dist/ScavRepFix-1.0.0.zip`. Omit `SkipDeploy=true`
to also copy the DLL into `BepInEx/plugins/Hysocs-ScavRepFix`.

## Compatibility

This version targets SPT 4.1.0 and checks for the expected SPT patch at startup.
If SPT changes or removes that patch, ScavRepFix logs an error and makes no
runtime modification.
