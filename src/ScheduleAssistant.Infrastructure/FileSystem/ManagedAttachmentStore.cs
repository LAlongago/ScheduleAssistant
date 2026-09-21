using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ScheduleAssistant.Application.Abstractions.Attachments;
using ScheduleAssistant.Application.Abstractions.Configuration;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.FileSystem;

/// <summary>
/// Stores attachment copies beneath one application-managed root and never mutates source files.
/// </summary>
public sealed class ManagedAttachmentStore : IAttachmentStore
{
    private const int MaximumSafeFileNameLength = 200;
    private static readonly HashSet<char> WindowsInvalidFileNameCharacters =
        new("<>:\\\"/\\\\|?*".ToCharArray());
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };
    private static readonly Dictionary<string, string> MimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".bmp"] = "image/bmp",
            [".csv"] = "text/csv",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".gif"] = "image/gif",
            [".jpeg"] = "image/jpeg",
            [".jpg"] = "image/jpeg",
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".rar"] = "application/vnd.rar",
            [".txt"] = "text/plain",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".zip"] = "application/zip"
        };

    private readonly IAppPaths _paths;
    private readonly Func<string, Stream> _sourceOpener;

    /// <summary>Initializes the store with the application path service.</summary>
    public ManagedAttachmentStore(IAppPaths paths)
        : this(paths, static path => new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan))
    {
    }

    /// <summary>
    /// Initializes the store with an injectable source opener for deterministic copy-failure tests.
    /// </summary>
    public ManagedAttachmentStore(IAppPaths paths, Func<string, Stream> sourceOpener)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _sourceOpener = sourceOpener ?? throw new ArgumentNullException(nameof(sourceOpener));
    }

    /// <inheritdoc />
    public async Task<StoredAttachment> ImportAsync(
        Guid taskId,
        Guid attachmentId,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(taskId, nameof(taskId));
        EnsureIdentity(attachmentId, nameof(attachmentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceFullPath = Path.GetFullPath(sourcePath);
        var displayName = Path.GetFileName(sourceFullPath);
        if (string.IsNullOrWhiteSpace(displayName) || displayName is "." or "..")
        {
            throw new ArgumentException("The source path must identify a file.", nameof(sourcePath));
        }

        var safeFileName = SanitizeFileName(displayName);
        var managedRelativePath = $"{taskId:D}/{attachmentId:D}_{safeFileName}";
        var taskDirectory = ResolveTaskDirectory(taskId);
        var finalPath = ResolveManagedPath(managedRelativePath);
        Directory.CreateDirectory(taskDirectory);

        var temporaryPath = Path.Combine(
            taskDirectory,
            $".tmp-{attachmentId:N}-{Guid.NewGuid():N}.part");
        var completed = false;
        try
        {
            await using var source = _sourceOpener(sourceFullPath)
                ?? throw new IOException("The source file could not be opened.");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long sizeBytes = 0;
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 64 * 1024,
                             options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, read);
                    sizeBytes = checked(sizeBytes + read);
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                destination.Flush(flushToDisk: true);
            }

            var sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

            // The temporary file and final file are in the same directory, so this move is atomic
            // on the file systems supported by the application.
            File.Move(temporaryPath, finalPath, overwrite: false);
            completed = true;

            var extension = Path.GetExtension(displayName);
            extension = string.IsNullOrWhiteSpace(extension)
                ? null
                : extension.ToLowerInvariant();
            return new StoredAttachment(
                attachmentId,
                taskId,
                displayName,
                managedRelativePath,
                extension,
                extension is not null && MimeTypes.TryGetValue(extension, out var mimeType)
                    ? mimeType
                    : "application/octet-stream",
                sizeBytes,
                sha256);
        }
        finally
        {
            if (!completed)
            {
                TryDeleteFile(temporaryPath);
            }
        }
    }

    /// <inheritdoc />
    public Task OpenAsync(string managedRelativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveManagedPath(managedRelativePath);
        EnsureExistingFile(fullPath);
        EnsureWindowsIntegration();
        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevealAsync(string managedRelativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveManagedPath(managedRelativePath);
        EnsureExistingFile(fullPath);
        EnsureWindowsIntegration();
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{fullPath}\"",
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string managedRelativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveManagedPath(managedRelativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteTaskDirectoryAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        EnsureIdentity(taskId, nameof(taskId));
        cancellationToken.ThrowIfCancellationRequested();
        var taskDirectory = ResolveTaskDirectory(taskId);
        if (Directory.Exists(taskDirectory))
        {
            Directory.Delete(taskDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListManagedFilesAsync(CancellationToken cancellationToken = default)
    {
        var root = GetAttachmentRoot();
        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(
                     root,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = false,
                         ReturnSpecialDirectories = false
                     }))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(file);
            EnsureWithinRoot(root, fullPath);
            EnsureNoLinkEscape(root, fullPath);
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith("../", StringComparison.Ordinal)
                || relative.Equals("..", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(Attachment.NormalizeManagedRelativePath(relative));
        }

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private string ResolveTaskDirectory(Guid taskId)
    {
        return ResolveManagedPath(taskId.ToString("D"), allowDirectory: true);
    }

    private string ResolveManagedPath(string managedRelativePath, bool allowDirectory = false)
    {
        var normalized = Attachment.NormalizeManagedRelativePath(managedRelativePath);
        var root = GetAttachmentRoot();
        var candidate = Path.GetFullPath(Path.Combine(
            root,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        EnsureWithinRoot(root, candidate);
        EnsureNoLinkEscape(root, candidate);
        if (!allowDirectory && string.Equals(candidate, root, GetPathComparison()))
        {
            throw new ArgumentException("A managed file path is required.", nameof(managedRelativePath));
        }

        return candidate;
    }

    private string GetAttachmentRoot()
    {
        var root = Path.GetFullPath(_paths.AttachmentsDirectory);
        Directory.CreateDirectory(root);
        var pathRoot = Path.GetPathRoot(root);
        return pathRoot is not null
            && string.Equals(root, pathRoot, GetPathComparison())
            ? root
            : root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void EnsureWithinRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The managed path escapes the attachment root.");
        }
    }

    private static void EnsureNoLinkEscape(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (relative is "." or "..")
        {
            return;
        }

        var current = root;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                continue;
            }

            FileSystemInfo? linkTarget = new FileInfo(current).ResolveLinkTarget(returnFinalTarget: true);
            linkTarget ??= new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true);

            if (linkTarget is not null)
            {
                EnsureWithinRoot(root, Path.GetFullPath(linkTarget.FullName));
            }
        }
    }

    private static void EnsureExistingFile(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The managed attachment file does not exist.");
        }
    }

    private static void EnsureWindowsIntegration()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Opening managed attachments requires Windows.");
        }
    }

    private static string SanitizeFileName(string displayName)
    {
        var builder = new StringBuilder(displayName.Length);
        foreach (var character in displayName)
        {
            builder.Append(
                char.IsControl(character)
                || WindowsInvalidFileNameCharacters.Contains(character)
                || Path.GetInvalidFileNameChars().Contains(character)
                    ? '_'
                    : character);
        }

        var name = builder.ToString().TrimEnd(' ', '.');
        if (name.Length == 0 || name is "." or "..")
        {
            name = "_";
        }

        var deviceStem = Path.GetFileNameWithoutExtension(name).TrimEnd(' ', '.');
        if (ReservedDeviceNames.Contains(deviceStem))
        {
            name = "_" + name;
        }

        if (name.Length <= MaximumSafeFileNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        var stemLength = Math.Max(1, MaximumSafeFileNameLength - extension.Length);
        var stem = name[..Math.Min(stemLength, name.Length - extension.Length)];
        if (stem.Length > 0 && char.IsHighSurrogate(stem[^1]))
        {
            stem = stem[..^1];
        }

        return stem + extension;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static void EnsureIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty identity is required.", parameterName);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The Application boundary records a cleanup item when final-file compensation fails.
        }
    }
}
