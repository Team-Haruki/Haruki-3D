using AssetStudio;
using PjskBundle2Parts.Models;
using Object = AssetStudio.Object;

namespace PjskBundle2Parts.Services;

public sealed class AssetStudioLoadedBundle : IDisposable
{
    private const string SekaiUnityVersion = "2022.3.21f1";

    private readonly DecryptedBundleWorkspace readableBundles;
    private readonly AssetsManager manager;

    public ResolvedBundleInput Input { get; private set; }
    public BundleLoadDependencyMode DependencyMode { get; }
    public IReadOnlyList<Object> Objects { get; }
    public IReadOnlyList<Object> PrimaryObjects { get; private set; }
    public int AssetsFileCount => manager.AssetsFileList.Count;

    private AssetStudioLoadedBundle(
        ResolvedBundleInput input,
        BundleLoadDependencyMode dependencyMode,
        DecryptedBundleWorkspace readableBundles,
        AssetsManager manager
    )
    {
        Input = input;
        DependencyMode = dependencyMode;
        this.readableBundles = readableBundles;
        this.manager = manager;
        Objects = manager.AssetsFileList
            .SelectMany(file => file.Objects)
            .ToList();
        PrimaryObjects = AssetStudioObjectFilter.SelectPrimaryObjects(Objects, readableBundles.PrimaryFileName);
    }

    public bool TrySelectInput(ResolvedBundleInput input)
    {
        if (!string.Equals(
                BundleDependencyResolver.ResolveLoadFamilyKey(Input, DependencyMode),
                BundleDependencyResolver.ResolveLoadFamilyKey(input, DependencyMode),
                StringComparison.Ordinal) ||
            !File.Exists(Path.Combine(readableBundles.DirectoryPath, Path.GetFileName(input.ResolvedBundlePath))))
        {
            return false;
        }
        Input = input;
        PrimaryObjects = AssetStudioObjectFilter.SelectPrimaryObjects(
            Objects,
            Path.GetFileName(input.ResolvedBundlePath)
        );
        return true;
    }

    public static AssetStudioLoadedBundle Load(
        ResolvedBundleInput input,
        BundleLoadDependencyMode dependencyMode = BundleLoadDependencyMode.Default,
        IReadOnlyList<string>? dependencyBundlePaths = null
    )
    {
        var loadPaths = BundleDependencyResolver
            .ResolveLoadBundlePaths(input, dependencyMode)
            .Concat(dependencyBundlePaths ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal);
        var readableBundles = new SekaiBundleDecryptor().PrepareReadableWorkspace(
            input.ResolvedBundlePath,
            loadPaths
        );
        var manager = new AssetsManager
        {
            MeshLazyLoad = false,
        };
        manager.Options.CustomUnityVersion = new UnityVersion(SekaiUnityVersion);
        manager.SetAssetFilter(
            ClassIDType.GameObject,
            ClassIDType.Transform,
            ClassIDType.Animator,
            ClassIDType.Material,
            ClassIDType.Mesh,
            ClassIDType.Texture2D,
            ClassIDType.MonoBehaviour,
            ClassIDType.MeshRenderer,
            ClassIDType.SkinnedMeshRenderer
        );
        manager.LoadFilesAndFolders(readableBundles.DirectoryPath);
        return new AssetStudioLoadedBundle(input, dependencyMode, readableBundles, manager);
    }

    public void Dispose()
    {
        readableBundles.Dispose();
    }
}
