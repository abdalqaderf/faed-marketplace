using Xunit;

// Integration tests share SQL Server LocalDB; creating/dropping databases concurrently is
// unreliable, so the assembly runs its collections sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
