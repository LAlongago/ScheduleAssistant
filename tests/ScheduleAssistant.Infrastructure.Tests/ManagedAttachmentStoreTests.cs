using ScheduleAssistant.Domain;
using ScheduleAssistant.Infrastructure.FileSystem;
using ScheduleAssistant.Infrastructure.Persistence;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class ManagedAttachmentStoreTests
{
    [Fact]
    public async Task Import_ShouldSupportUnicodeSpacesLongNamesAndSameNamesWithoutDeletingSources()
    {
        var root = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(root);
            var store = new ManagedAttachmentStore(paths);
            var firstSource = Path.Combine(root, "sources", "one", "会议 资料.txt");
            var secondSource = Path.Combine(root, "sources", "two", "会议 资料.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(firstSource)!);
            Directory.CreateDirectory(Path.GetDirectoryName(secondSource)!);
            File.WriteAllText(firstSource, "第一份资料🙂");
            File.WriteAllText(secondSource, "第二份资料\0\u0001");
            var longSource = Path.Combine(root, "sources", new string('长', 220) + ".bin");
            File.WriteAllBytes(longSource, [0, 1, 2, 3, 4, 255]);
            var taskId = Guid.Parse("77777777-7777-7777-7777-777777777777");

            var first = await store.ImportAsync(
                taskId,
                Guid.Parse("88888888-8888-8888-8888-888888888888"),
                firstSource);
            var second = await store.ImportAsync(
                taskId,
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                secondSource);
            var longName = await store.ImportAsync(
                taskId,
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                longSource);

            Assert.Equal("会议 资料.txt", first.DisplayName);
            Assert.Equal("会议 资料.txt", second.DisplayName);
            Assert.NotEqual(first.ManagedRelativePath, second.ManagedRelativePath);
            Assert.Equal(".bin", longName.Extension);
            Assert.True(longName.ManagedRelativePath.Length < 300);
            Assert.Equal(new FileInfo(firstSource).Length, first.SizeBytes);
            Assert.Equal(new FileInfo(secondSource).Length, second.SizeBytes);
            Assert.Equal(new FileInfo(longSource).Length, longName.SizeBytes);
            Assert.Equal("第一份资料🙂", File.ReadAllText(GetManagedPath(paths, first.ManagedRelativePath)));
            Assert.Equal("第二份资料\0\u0001", File.ReadAllText(GetManagedPath(paths, second.ManagedRelativePath)));
            Assert.Equal([0, 1, 2, 3, 4, 255], File.ReadAllBytes(GetManagedPath(paths, longName.ManagedRelativePath)));
            Assert.True(File.Exists(firstSource));
            Assert.True(File.Exists(secondSource));
            Assert.True(File.Exists(longSource));
            Assert.DoesNotContain(
                await store.ListManagedFilesAsync(),
                path => path.Contains(".tmp-", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task Import_WhenSourceDisappearsDuringCopy_ShouldDeleteTemporaryFile()
    {
        var root = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(root);
            var store = new ManagedAttachmentStore(paths, _ => new DisappearingStream());

            await Assert.ThrowsAsync<FileNotFoundException>(() => store.ImportAsync(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Path.Combine(root, "source.bin")));

            var files = Directory.Exists(paths.AttachmentsDirectory)
                ? Directory.GetFiles(paths.AttachmentsDirectory, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
            Assert.Empty(files);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ManagedOperations_WhenPathEscapesRoot_ShouldRejectIt()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new ManagedAttachmentStore(new AppPaths(root));

            await Assert.ThrowsAsync<DomainValidationException>(() =>
                store.OpenAsync("../outside.txt"));
            await Assert.ThrowsAsync<DomainValidationException>(() =>
                store.DeleteAsync("task/../../outside.txt"));
            await Assert.ThrowsAsync<DomainValidationException>(() =>
                store.RevealAsync("/outside.txt"));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV070-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetManagedPath(AppPaths paths, string relativePath)
    {
        return Path.Combine(paths.AttachmentsDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class DisappearingStream : MemoryStream
    {
        private bool _hasRead;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_hasRead)
            {
                return ValueTask.FromException<int>(
                    new FileNotFoundException("The source disappeared while it was copied."));
            }

            _hasRead = true;
            buffer.Span[0] = 0x42;
            return ValueTask.FromResult(1);
        }
    }
}
