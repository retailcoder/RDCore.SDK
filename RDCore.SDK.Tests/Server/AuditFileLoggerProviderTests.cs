using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RDCore.SDK.Platform;
using RDCore.SDK.Runtime;
using RDCore.SDK.Server.Auditing;
using RDCore.SDK.Server.Configuration;

namespace RDCore.Tests.Server;

/// <summary>
/// The audit log's file: a line of JSON an entry, in a file of each process for each day (UTC), and nothing that is not an audit entry.
/// </summary>
[TestClass]
public sealed class AuditFileLoggerProviderTests
{
    private readonly MockFileSystem _fileSystem = new();

    private string Folder => _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "audits");

    private static AuditEntry Call(DateTimeOffset time) => new()
    {
        Time = time,
        Component = "EnvironmentHost",
        Process = 42,
        Module = "Module1",
        Procedure = "Main",
        Line = 3,
        Category = ExecutionCategory.ProcessLaunch,
        Target = "VBA.Interaction.Shell",
        Decision = AuditDecision.Halted,
        Mode = ExecutionPolicyMode.Halt,
    };

    private static void Write(ILogger logger, AuditEntry entry)
        => logger.Log(LogLevel.Information, AuditLog.Event, new AuditLogState(entry), null, static (state, _) => state.ToString());

    private string[] Lines(string fileName) => _fileSystem.File.ReadAllLines(_fileSystem.Path.Combine(Folder, fileName));

    [TestMethod]
    public void AnEntry_IsALineOfJson_InCamelCase_WithItsEnumerationsByName_AndNoMemberItDoesNotKnow()
    {
        using var provider = new AuditFileLoggerProvider(_fileSystem, Folder, "EnvironmentHost", 42);
        var time = new DateTimeOffset(2026, 11, 26, 23, 30, 0, TimeSpan.Zero);

        Write(provider.CreateLogger(AuditLog.Category), Call(time));

        var line = Lines(provider.FileNameOf(new DateOnly(2026, 11, 26))).Single();
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        Assert.AreEqual("VBA.Interaction.Shell", root.GetProperty("target").GetString());
        Assert.AreEqual("ProcessLaunch", root.GetProperty("category").GetString());
        Assert.AreEqual("Halted", root.GetProperty("decision").GetString());
        Assert.AreEqual(3, root.GetProperty("line").GetInt32());
        Assert.AreEqual(time, root.GetProperty("time").GetDateTimeOffset());
        Assert.IsFalse(root.TryGetProperty("session", out _));
        Assert.IsFalse(root.TryGetProperty("dropped", out _));
    }

    [TestMethod]
    public void TheFileOfAnEntry_IsTheFileOfItsDayInUtc_ForItsProcess()
    {
        using var provider = new AuditFileLoggerProvider(_fileSystem, Folder, "EnvironmentHost", 42);
        var logger = provider.CreateLogger(AuditLog.Category);

        // 20:30 on the 26th in Québec is 01:30 on the 27th in UTC.
        Write(logger, Call(new DateTimeOffset(2026, 11, 26, 20, 30, 0, TimeSpan.FromHours(-5))));
        Write(logger, Call(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero)));

        Assert.AreEqual("audit-20261127-EnvironmentHost-42.jsonl", provider.FileNameOf(new DateOnly(2026, 11, 27)));
        Assert.HasCount(1, Lines(provider.FileNameOf(new DateOnly(2026, 11, 27))));
        Assert.HasCount(1, Lines(provider.FileNameOf(new DateOnly(2026, 11, 26))));
    }

    [TestMethod]
    public void WhatAnotherLoggerWrites_IsNotWritten()
    {
        using var provider = new AuditFileLoggerProvider(_fileSystem, Folder, "EnvironmentHost", 42);

        provider.CreateLogger(AuditLog.Category).LogInformation("not an entry");

        Assert.IsFalse(_fileSystem.Directory.Exists(Folder));
    }

    [TestMethod]
    public void AnEntryWrittenOnceTheProviderIsDisposed_Throws()
    {
        var provider = new AuditFileLoggerProvider(_fileSystem, Folder, "EnvironmentHost", 42);
        var logger = provider.CreateLogger(AuditLog.Category);
        provider.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => Write(logger, Call(ManualClock.Start)));
    }

    [TestMethod]
    [DataRow("", null, DisplayName = "none: the platform's audits folder")]
    [DataRow("audits", "audits", DisplayName = "relative: under the platform root")]
    public void TheFolderOfTheFiles_IsTheOneTheOptionsSay(string directory, string? underRoot)
    {
        var environment = new PlatformEnvironment(_fileSystem);

        var folder = AuditServiceCollectionExtensions.DirectoryOf(new SdkAuditFileOptions { Directory = directory }, environment);

        Assert.AreEqual(underRoot is null ? environment.AuditsDirectory : environment.Resolve(underRoot), folder);
    }

    [TestMethod]
    public void ARootedFolderOfTheFiles_IsTakenAsItIs()
    {
        var rooted = _fileSystem.Path.GetFullPath(Folder);

        var folder = AuditServiceCollectionExtensions.DirectoryOf(new SdkAuditFileOptions { Directory = rooted }, new PlatformEnvironment(_fileSystem));

        Assert.AreEqual(rooted, folder);
    }

    [TestMethod]
    public void AComponentsAuditLog_WritesToTheFileItsOptionsName_AndToEveryAuditProviderRegistered()
    {
        var keyed = new RecordingProvider();
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new SdkAppOptions { Audit = new() { File = new() { Directory = Folder } } }))
            .AddSingleton<System.IO.Abstractions.IFileSystem>(_fileSystem)
            .AddSingleton<IPlatformEnvironment>(new PlatformEnvironment(_fileSystem))
            .AddKeyedSingleton<ILoggerProvider>(AuditLog.ProviderKey, keyed)
            .AddAuditLog();

        using (var provider = services.BuildServiceProvider())
        {
            var log = provider.GetRequiredService<IAuditLog>();
            Assert.IsTrue(log.IsEnabled);
            log.Record(Call(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero)));
        }

        Assert.HasCount(1, keyed.Entries);
        var file = Assert.ContainsSingle(_fileSystem.Directory.GetFiles(Folder));
        Assert.HasCount(1, _fileSystem.File.ReadAllLines(file));
    }

    [TestMethod]
    public void AComponentWithNothingToWriteTo_HasTheLogOfNothing()
    {
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new SdkAppOptions { Audit = new() { File = new() { Enabled = false } } }))
            .AddAuditLog();

        using var provider = services.BuildServiceProvider();

        Assert.AreSame(AuditLog.None, provider.GetRequiredService<IAuditLog>());
    }

    private sealed class RecordingProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<AuditEntry> _entries = new();

        public AuditEntry[] Entries => [.. _entries];

        public ILogger CreateLogger(string categoryName) => new Logger(_entries);

        public void Dispose()
        {
        }

        private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<AuditEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (state is AuditLogState audit)
                {
                    entries.Enqueue(audit.Entry);
                }
            }
        }
    }
}
