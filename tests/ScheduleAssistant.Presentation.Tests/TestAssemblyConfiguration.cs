using Xunit;

// WPF application and dispatcher state is process-wide; execute Presentation test classes serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
