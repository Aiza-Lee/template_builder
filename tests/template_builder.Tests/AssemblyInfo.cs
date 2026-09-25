using Xunit;

// Disable cross-class parallelization to avoid race conditions on global state (e.g. I18n, TerminalWidthRegistry)
[assembly: CollectionBehavior(DisableTestParallelization = true)]
