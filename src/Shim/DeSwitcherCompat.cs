using System;
using UnityEngine;

public static class DeSwitcherCompat
{
    public static Action<SpawnableAsset, GameObject> BeforeSpawn;
    public static Action<SpawnableAsset, GameObject> AfterSpawn;

    public static void PerformBeforeSpawn(SpawnableAsset asset, GameObject instance) => BeforeSpawn?.Invoke(asset, instance);

    public static void PerformAfterSpawn(SpawnableAsset asset, GameObject instance) => AfterSpawn?.Invoke(asset, instance);

    public static void PerformMod(SpawnableAsset asset, GameObject instance) => AfterSpawn?.Invoke(asset, instance);
}
