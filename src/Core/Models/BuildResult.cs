using System;
using System.Collections.Generic;
using System.IO;

namespace Core.Models {
    /// <summary>
    /// 构建执行状态。
    /// </summary>
    internal enum BuildStatus {
        Success,
        SuccessWithWarnings,
        Failed,
        UnresolvedPlaceholders,
        Timeout,
        Cancelled
    }

    /// <summary>
    /// 统一结构化构建结果，包含最终状态、耗时、产物路径、诊断信息与警告。
    /// 供 CLI、TUI、验证器与自动化脚本统一展示与判定。
    /// </summary>
    internal sealed class BuildResult {
        public BuildStatus Status { get; init; }
        public int ExitCode { get; init; }
        public FileInfo? OutputPdf { get; init; }
        public string? IntermediateTexPath { get; init; }
        public string? LogFilePath { get; init; }
        public int PassesCompleted { get; init; }
        public int TotalPasses { get; init; }
        public TimeSpan Duration { get; init; }
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public string? ErrorMessage { get; init; }
        public string? DiagnosticPdfPath { get; init; }
        public string? CulpritSourceFile { get; init; }
        public int? CulpritLineNumber { get; init; }
        public string? FailureReason { get; init; }
        public string? Recommendation { get; init; }

        public bool IsSuccess => Status is BuildStatus.Success or BuildStatus.SuccessWithWarnings;

        public static BuildResult CreateSuccess(
            FileInfo outputPdf,
            string? texPath,
            string? logPath,
            int passes,
            TimeSpan duration,
            IReadOnlyList<string>? warnings = null
        ) => new() {
            Status = warnings is { Count: > 0 } ? BuildStatus.SuccessWithWarnings : BuildStatus.Success,
            ExitCode = ExitCodes.Success,
            OutputPdf = outputPdf,
            IntermediateTexPath = texPath,
            LogFilePath = logPath,
            PassesCompleted = passes,
            TotalPasses = passes,
            Duration = duration,
            Warnings = warnings ?? Array.Empty<string>()
        };

        public static BuildResult CreateFailure(
            int exitCode,
            BuildStatus status,
            string errorMessage,
            IReadOnlyList<string>? diagnostics = null,
            string? logPath = null,
            TimeSpan duration = default,
            int passesCompleted = 0,
            string? diagnosticPdfPath = null,
            string? culpritSourceFile = null,
            int? culpritLineNumber = null,
            string? failureReason = null,
            string? recommendation = null
        ) => new() {
            Status = status,
            ExitCode = exitCode,
            ErrorMessage = errorMessage,
            Diagnostics = diagnostics ?? Array.Empty<string>(),
            LogFilePath = logPath,
            Duration = duration,
            PassesCompleted = passesCompleted,
            DiagnosticPdfPath = diagnosticPdfPath,
            CulpritSourceFile = culpritSourceFile,
            CulpritLineNumber = culpritLineNumber,
            FailureReason = failureReason,
            Recommendation = recommendation
        };
    }
}
