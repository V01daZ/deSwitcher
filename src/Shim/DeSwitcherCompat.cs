using System;
using System.IO;
using UnityEngine;

public static class DeSwitcherCompat
{
    public static Action<SpawnableAsset, GameObject> BeforeSpawn;
    public static Action<SpawnableAsset, GameObject> AfterSpawn;

    public static void PerformBeforeSpawn(SpawnableAsset asset, GameObject instance) => BeforeSpawn?.Invoke(asset, instance);

    public static void PerformAfterSpawn(SpawnableAsset asset, GameObject instance) => AfterSpawn?.Invoke(asset, instance);

    public static void PerformMod(SpawnableAsset asset, GameObject instance) => AfterSpawn?.Invoke(asset, instance);

    public static void PerformAfterSpawn(string assetName, GameObject instance)
    {
        var asset = CatalogBehaviour.Main == null ? null : CatalogBehaviour.Main.GetSpawnable(assetName);
        if (asset != null)
            PerformAfterSpawn(asset, instance);
    }

    public static void PerformMod(string assetName, GameObject instance) => PerformAfterSpawn(assetName, instance);
}

public static class DeSwitcherLoaderCompat
{
    public static System.Threading.Tasks.Task<(ModCompilationResult result, ModScript script)> CompileMod(this BackgroundItemLoader loader, ModMetaData meta)
    {
        string message = Application.systemLanguage == SystemLanguage.Russian
            ? "deSwitcher: на 1.28 моды нельзя перекомпилировать во время игры. Перезапустите игру."
            : "deSwitcher: mods can't be recompiled while the game runs on 1.28. Restart the game.";
        Debug.LogWarning(message);
        return System.Threading.Tasks.Task.FromResult((new ModCompilationResult(false, message, false), new ModScript()));
    }

    public static void ProcessModCompilationResult(this BackgroundItemLoader loader, (ModCompilationResult result, ModScript script) compiled, ModMetaData meta)
    {
    }

    public static System.Collections.IEnumerator DoAsyncTask(this BackgroundItemLoader loader, Func<System.Threading.Tasks.Task> action, string name, string description, Sprite sprite = null) =>
        BackgroundItemLoader.DoAsyncTask(action, name, description, sprite);
}

public static class DeSwitcherContraptions
{
    public const string Path = "Contraptions/";

    public static void SaveThumbnail(ObjectState[] states, string name) => ContraptionSerialiser.SaveThumbnail(Meta(name), states);

    public static void SaveThumbnail(ContraptionMetaData metadata, ObjectState[] states) => ContraptionSerialiser.SaveThumbnail(metadata, states);

    public static void SaveOutline(string saveName, GameObject[] gameObjects, Vector3 center) => ContraptionOutlineSerialiser.SaveOutline(Meta(saveName), gameObjects, center);

    public static void SaveOutline(ContraptionMetaData metadata, GameObject[] gameObjects, Vector3 center) => ContraptionOutlineSerialiser.SaveOutline(metadata, gameObjects, center);

    public static ContraptionMetaData Meta(string name)
    {
        if (string.IsNullOrEmpty(name) || name != System.IO.Path.GetFileName(name) || name == "." || name == "..")
            throw new ArgumentException("Invalid contraption name: " + name);
        var dir = new DirectoryInfo(System.IO.Path.Combine(ContraptionSerialiser.SaveDirectory.FullName, name));
        dir.Create();
        return new ContraptionMetaData(name)
        {
            Directory = dir,
            PathToMetadata = System.IO.Path.Combine(dir.FullName, name + ".json"),
            PathToDataFile = System.IO.Path.Combine(dir.FullName, name + ".jaap"),
            PathToThumbnail = System.IO.Path.Combine(dir.FullName, name + ".png"),
            PathToOutlineFile = System.IO.Path.Combine(dir.FullName, name + ".outline"),
        };
    }
}
