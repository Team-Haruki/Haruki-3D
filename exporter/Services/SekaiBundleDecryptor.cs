namespace PjskBundle2Parts.Services;

public sealed class SekaiBundleDecryptor
{
    private static readonly byte[] SekaiMagic = { 0x10, 0x00, 0x00, 0x00 };

    // Unity bundle signatures accepted by AssetStudio's FileReader.CheckFileType.
    private static readonly byte[][] UnityBundleSignatures =
    {
        "UnityFS"u8.ToArray(),
        "UnityWeb"u8.ToArray(),
        "UnityRaw"u8.ToArray(),
        "UnityArchive"u8.ToArray(),
    };

    // Longest recognized header prefix: "UnityArchive".
    private const int HeaderProbeLength = 12;

    public DecryptedBundleHandle PrepareReadableBundle(string bundlePath)
    {
        using var source = File.Open(bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> header = stackalloc byte[HeaderProbeLength];
        var probe = ReadHeaderProbe(source, header);

        if (IsSekaiWrapped(probe))
        {
            var tempPath = CreateTempSiblingPath(bundlePath);
            try
            {
                using var target = File.Create(tempPath);
                DecryptTo(source, target);
                EnsureRecognizedUnityBundle(target, bundlePath);
                return new DecryptedBundleHandle(tempPath, deleteOnDispose: true);
            }
            catch
            {
                File.Delete(tempPath);
                throw;
            }
        }

        if (!IsRecognizedUnityBundle(probe))
        {
            ThrowUnrecognizedBundle(probe, bundlePath, deobfuscated: false);
        }
        return new DecryptedBundleHandle(bundlePath, deleteOnDispose: false);
    }

    public DecryptedBundleWorkspace PrepareReadableWorkspace(string primaryBundlePath, IEnumerable<string> bundlePaths)
    {
        var normalizedPrimary = Path.GetFullPath(primaryBundlePath);
        var sourcePaths = bundlePaths
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => string.Equals(path, normalizedPrimary, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .GroupBy(NormalizeReadableFileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (!sourcePaths.Contains(normalizedPrimary, StringComparer.Ordinal))
        {
            sourcePaths.Insert(0, normalizedPrimary);
        }

        var workspacePath = Path.Combine(Path.GetTempPath(), $"pjskbundle2parts.workspace.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);

        try
        {
            foreach (var sourcePath in sourcePaths)
            {
                var targetPath = Path.Combine(workspacePath, NormalizeReadableFileName(sourcePath));
                PrepareReadableBundleFile(
                    sourcePath,
                    targetPath,
                    isPrimary: string.Equals(sourcePath, normalizedPrimary, StringComparison.Ordinal)
                );
            }
        }
        catch
        {
            TryDeleteDirectory(workspacePath);
            throw;
        }

        return new DecryptedBundleWorkspace(
            workspacePath,
            Path.Combine(workspacePath, NormalizeReadableFileName(normalizedPrimary))
        );
    }

    private void PrepareReadableBundleFile(string sourcePath, string targetPath, bool isPrimary)
    {
        using var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Span<byte> header = stackalloc byte[HeaderProbeLength];
        var probe = ReadHeaderProbe(source, header);

        if (IsSekaiWrapped(probe))
        {
            using var target = File.Create(targetPath);
            DecryptTo(source, target);
            EnsureRecognizedUnityBundle(target, sourcePath);
            return;
        }

        if (!IsRecognizedUnityBundle(probe))
        {
            if (isPrimary)
            {
                ThrowUnrecognizedBundle(probe, sourcePath, deobfuscated: false);
            }
            // Zero-byte non-primary siblings are legitimate sparse-input placeholders swept
            // in by directory-based dependency resolution; AssetStudio ignores them as
            // resources. Other unrecognized siblings are also swept in by that directory
            // scan and classified by AssetStudio as ignorable resource files, so copy them
            // through with a warning instead of aborting the unrelated primary bundle.
            if (probe.Length > 0)
            {
                WarnUnrecognizedSiblingBundle(probe, sourcePath);
            }
        }

        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static ReadOnlySpan<byte> ReadHeaderProbe(Stream source, Span<byte> header)
    {
        var read = source.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        source.Position = 0;
        return header[..read];
    }

    private static bool IsSekaiWrapped(ReadOnlySpan<byte> probe) =>
        probe.Length >= SekaiMagic.Length && probe[..SekaiMagic.Length].SequenceEqual(SekaiMagic);

    private static bool IsRecognizedUnityBundle(ReadOnlySpan<byte> probe)
    {
        foreach (var signature in UnityBundleSignatures)
        {
            if (probe.Length >= signature.Length && probe[..signature.Length].SequenceEqual(signature))
            {
                return true;
            }
        }
        return false;
    }

    private static void EnsureRecognizedUnityBundle(Stream decrypted, string bundlePath)
    {
        Span<byte> header = stackalloc byte[HeaderProbeLength];
        var probe = ReadHeaderProbe(decrypted, header);
        if (!IsRecognizedUnityBundle(probe))
        {
            ThrowUnrecognizedBundle(probe, bundlePath, deobfuscated: true);
        }
    }

    private static void ThrowUnrecognizedBundle(ReadOnlySpan<byte> probe, string bundlePath, bool deobfuscated)
    {
        var firstBytes = probe.Length == 0 ? "(empty file)" : $"0x{Convert.ToHexString(probe)}";
        throw new InvalidDataException(
            $"Unrecognized bundle format for '{bundlePath}'" +
            (deobfuscated ? " after PJSK wrapper deobfuscation" : string.Empty) +
            $": first bytes {firstBytes} match neither the known PJSK obfuscation wrapper nor a " +
            "Unity bundle signature (UnityFS/UnityWeb/UnityRaw/UnityArchive). " +
            "The game may have shipped a new bundle obfuscation format."
        );
    }

    private static void WarnUnrecognizedSiblingBundle(ReadOnlySpan<byte> probe, string bundlePath)
    {
        Console.Error.WriteLine(
            $"Unrecognized sibling bundle copied through as a resource file: '{bundlePath}': " +
            $"first bytes 0x{Convert.ToHexString(probe)} match neither the known PJSK obfuscation " +
            "wrapper nor a Unity bundle signature (UnityFS/UnityWeb/UnityRaw/UnityArchive)."
        );
    }

    private static string NormalizeReadableFileName(string sourcePath)
    {
        return Path.GetFileName(sourcePath);
    }

    private static void DecryptTo(Stream source, Stream target)
    {
        Span<byte> magic = stackalloc byte[4];
        if (source.Read(magic) != 4 || !magic.SequenceEqual(SekaiMagic))
        {
            source.Position = 0;
            source.CopyTo(target);
            target.Position = 0;
            return;
        }

        var encryptedHeader = new byte[128];
        var actualHeaderBytes = source.Read(encryptedHeader, 0, encryptedHeader.Length);
        if (actualHeaderBytes != encryptedHeader.Length)
        {
            throw new InvalidDataException("Encrypted bundle header is shorter than 128 bytes.");
        }

        for (var i = 0; i < encryptedHeader.Length; i += 8)
        {
            for (var j = 0; j < 5; j++)
            {
                encryptedHeader[i + j] = (byte)~encryptedHeader[i + j];
            }
        }

        target.Write(encryptedHeader, 0, encryptedHeader.Length);
        source.CopyTo(target);
        target.Position = 0;
    }

    private static string CreateTempSiblingPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath)
            ?? throw new InvalidOperationException($"Cannot determine bundle directory for {originalPath}");
        var fileName = Path.GetFileName(originalPath);
        var tempName = $".pjskbundle2parts.{Guid.NewGuid():N}.{fileName}";
        return Path.Combine(directory, tempName);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Keep best-effort cleanup silent for converter probing.
        }
    }

}

public sealed class DecryptedBundleWorkspace : IDisposable
{
    public string DirectoryPath { get; }
    public string PrimaryPath { get; }
    public string PrimaryFileName => Path.GetFileName(PrimaryPath);

    public DecryptedBundleWorkspace(string directoryPath, string primaryPath)
    {
        DirectoryPath = directoryPath;
        PrimaryPath = primaryPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
        catch
        {
            // Keep best-effort cleanup silent for converter probing.
        }
    }
}

public sealed class DecryptedBundleHandle : IDisposable
{
    public string Path { get; }

    private readonly bool deleteOnDispose;

    public DecryptedBundleHandle(string path, bool deleteOnDispose)
    {
        Path = path;
        this.deleteOnDispose = deleteOnDispose;
    }

    public void Dispose()
    {
        if (!deleteOnDispose)
        {
            return;
        }

        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch
        {
            // Keep best-effort cleanup silent for converter probing.
        }
    }
}
