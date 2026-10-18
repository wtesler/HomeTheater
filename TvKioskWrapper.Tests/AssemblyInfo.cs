using Xunit;

// Disable parallel test execution across collections so the shared localhost HTTP automation
// server on port 9333 never conflicts between test suites.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

