using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RDCore.SDK;
using RDCore.SDK.Client;
using RDCore.SDK.Server;
using RDCore.SDK.Server.Services;
using RDCore.SDK.Server.Services.States;

namespace RDCore.Tests.Server;

[TestClass]
public class AppHostRegistrationTests
{
    private static IServiceProvider BuildExternalServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        new TestAppHost().Configure(services, configuration);
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public void ServerStateProvider_IsRegisteredAsASingleton()
    {
        var provider = BuildExternalServices();

        var first = provider.GetRequiredService<IServerStateProvider>();
        var second = provider.GetRequiredService<IServerStateProvider>();

        // the server app, the LSP lifecycle handlers and the health check coordinate through this
        // instance's state and token sources; separate instances break graceful shutdown.
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void ServerProcess_StaysTransient()
    {
        var provider = BuildExternalServices();

        Assert.AreNotSame(
            provider.GetRequiredService<IRDCoreServerProcess>(),
            provider.GetRequiredService<IRDCoreServerProcess>());
    }

    // only Windows has the event log: elsewhere the generic host adds no provider of it, and there is nothing to remove.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AComponent_DoesNotLogToTheWindowsEventLog()
    {
        // the services as the generic host makes them, which on Windows log warnings and errors to the Application event log.
        var services = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder().Services;
        var configuration = new ConfigurationBuilder().Build();

        new TestAppHost().Configure(services, configuration);

        Assert.IsFalse(services.Any(descriptor => descriptor.ServiceType == typeof(ILoggerProvider)
            && descriptor.ImplementationType == typeof(Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider)));
    }

    // a component's file log has a rule of its own for every category, which is chosen over one that names only the category.
    [TestMethod]
    [DataRow(false, DisplayName = "a component without a file log")]
    [DataRow(true, DisplayName = "a component with its file log")]
    public void AComponent_DoesNotLogTheWarningOfAMessageHeldUntilTheClientIsInitialized(bool fileLog)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // a provider that writes whatever it is given: what is not written is what the component's logging filters out.
        var writesEverything = Substitute.For<ILogger>();
        writesEverything.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var everything = Substitute.For<ILoggerProvider>();
        everything.CreateLogger(Arg.Any<string>()).Returns(writesEverything);
        services.AddLogging(builder => builder.AddProvider(everything));
        new TestAppHost(fileLog ? $"rdcore-tests-held-warning-{Guid.NewGuid():N}.log" : null).Configure(services, configuration);

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(RDCore.SDK.Server.Logging.DeferredOutputLogging.Category);

        Assert.IsFalse(logger.IsEnabled(LogLevel.Warning), "OmniSharp warns of each message it holds, and sends it later");
        Assert.IsTrue(logger.IsEnabled(LogLevel.Error));
    }

    [TestMethod]
    public void TheLibrarySource_ReadsThePlatformsSymbolsFolder()
        => Assert.IsInstanceOfType<RDCore.SDK.Runtime.Libraries.DirectoryLibrarySource>(BuildExternalServices().GetRequiredService<RDCore.SDK.Runtime.Libraries.ILibrarySource>());

    [TestMethod]
    public void AnApplicationsLibrarySource_IsGivenTheSymbolsFolders_AndIsTheOneEveryServiceGets()
    {
        RDCore.SDK.Runtime.Libraries.ILibrarySource? given = null;
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        new TestAppHost(libraries: symbols => new RDCore.SDK.Runtime.Libraries.CompositeLibrarySource(given = symbols)).Configure(services, configuration);
        using var provider = services.BuildServiceProvider();

        var source = provider.GetRequiredService<RDCore.SDK.Runtime.Libraries.ILibrarySource>();

        Assert.IsInstanceOfType<RDCore.SDK.Runtime.Libraries.DirectoryLibrarySource>(given);
        CollectionAssert.AreEqual(new[] { given }, ((RDCore.SDK.Runtime.Libraries.CompositeLibrarySource)source).Sources.ToArray());
        Assert.AreSame(source, provider.GetRequiredService<RDCore.SDK.Runtime.Libraries.ILibrarySource>());
    }

    // the shipped default: every call made and none audited, which is what a platform that states no policy runs with.
    [TestMethod]
    public void AComponentThatStatesNoPolicy_AllowsEveryCall_AndAuditsNone()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        new TestAppHost().Configure(services, configuration);
        using var provider = services.BuildServiceProvider();

        var policy = provider.GetRequiredService<RDCore.SDK.Runtime.Abstract.Execution.IRuntimeEnvironmentProfile>().ExecutionPolicy;

        Assert.IsTrue(policy.IsPassThrough);
        Assert.AreEqual(RDCore.SDK.Runtime.ExecutionPolicy.Default, policy);
        Assert.IsNotNull(provider.GetService<RDCore.SDK.Server.Auditing.IAuditLog>(), "the component has an audit log for a policy that audits");
    }

    // exposes AppHost's protected external-service registration for assertions.
    private sealed class TestAppHost(string? logFileName = null,
        Func<RDCore.SDK.Runtime.Libraries.ILibrarySource, RDCore.SDK.Runtime.Libraries.ILibrarySource>? libraries = null) : AppHost<TestAppHost.FakeApp>
    {
        protected override string? LogFileName => logFileName;

        protected override RDCore.SDK.Runtime.Libraries.ILibrarySource LibrarySource(IServiceProvider provider, RDCore.SDK.Runtime.Libraries.ILibrarySource symbols)
            => libraries is null ? base.LibrarySource(provider, symbols) : libraries(symbols);

        public void Configure(IServiceCollection services, IConfiguration configuration)
            => ConfigureExternalServices(services, configuration);

        protected override void Configure(IConfigurationBuilder configuration, IServiceCollection services, string[] args) { }

        internal sealed class FakeApp : IRDCoreApp
        {
            public CoreServerComponent PlatformComponent => CoreServerComponent.LanguageServer;
            public Task RunAsync(IServiceProvider provider, string[] args) => Task.CompletedTask;
            public void LogIfEnabled(LogLevel logLevel, string message) { }
            public void Dispose() { }
        }
    }
}
