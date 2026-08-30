using System.Collections.Immutable;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Generators.Analyzers;
using AtomUI.City.Generators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Generators.Tests;

public sealed class BuildServiceProviderUsageAnalyzerTests
{
    [Theory]
    [InlineData("context.Services.BuildServiceProvider();")]
    [InlineData("context.Services.BuildServiceProvider(validateScopes: true);")]
    [InlineData("context.Services.BuildServiceProvider(new ServiceProviderOptions());")]
    [InlineData("((IServiceCollection)context.Services).BuildServiceProvider();")]
    [InlineData("((IServiceCollection)context.Services).BuildServiceProvider(validateScopes: true);")]
    [InlineData("((IServiceCollection)context.Services).BuildServiceProvider(new ServiceProviderOptions());")]
    [InlineData("ServiceCollectionContainerBuilderExtensions.BuildServiceProvider((IServiceCollection)context.Services);")]
    [InlineData("new ServiceCollection().BuildServiceProvider();")]
    [InlineData("System.Func<IServiceCollection, ServiceProvider> build = ServiceCollectionContainerBuilderExtensions.BuildServiceProvider; _ = build(context.Services);")]
    [InlineData("System.Func<IServiceCollection, bool, ServiceProvider> build = ServiceCollectionContainerBuilderExtensions.BuildServiceProvider; _ = build(context.Services, true);")]
    [InlineData("System.Func<IServiceCollection, ServiceProviderOptions, ServiceProvider> build = ServiceCollectionContainerBuilderExtensions.BuildServiceProvider; _ = build(context.Services, new ServiceProviderOptions());")]
    [InlineData("System.Func<ModuleServiceCollection, ServiceProvider> build = ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider; _ = build(context.Services);")]
    public async Task AnalyzerRejectsProviderBuildEntrypoints(string statement)
    {
        var source = CreateModuleSource(statement);

        var diagnostic = Assert.Single(await GetAnalyzerDiagnosticsAsync(source));

        Assert.Equal(AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(
            "BuildServiceProvider",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("new DefaultServiceProviderFactory().CreateServiceProvider(context.Services);")]
    [InlineData("IServiceProviderFactory<IServiceCollection> factory = new DefaultServiceProviderFactory(); _ = factory.CreateServiceProvider(context.Services);")]
    [InlineData("System.Func<IServiceCollection, System.IServiceProvider> create = new DefaultServiceProviderFactory().CreateServiceProvider; _ = create(context.Services);")]
    public async Task AnalyzerRejectsServiceProviderFactoryEntrypoints(string statement)
    {
        var diagnostic = Assert.Single(await GetAnalyzerDiagnosticsAsync(CreateModuleSource(statement)));

        Assert.Equal(AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(
            "CreateServiceProvider",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("_ = Host.CreateApplicationBuilder().Build();")]
    [InlineData("_ = Host.CreateDefaultBuilder().Build();")]
    [InlineData("_ = new HostBuilder().Build();")]
    [InlineData("IHostBuilder builder = new HostBuilder(); _ = builder.Build();")]
    [InlineData("System.Func<IHost> build = Host.CreateApplicationBuilder().Build; _ = build();")]
    [InlineData("IHostBuilder builder = new HostBuilder(); System.Func<IHost> build = builder.Build; _ = build();")]
    [InlineData("_ = Host.CreateDefaultBuilder().Start();")]
    [InlineData("_ = Host.CreateDefaultBuilder().StartAsync();")]
    [InlineData("_ = Host.CreateDefaultBuilder().RunConsoleAsync();")]
    [InlineData("_ = HostingAbstractionsHostBuilderExtensions.Start(new HostBuilder());")]
    [InlineData("IHostBuilder builder = new HostBuilder(); System.Func<IHost> start = builder.Start; _ = start();")]
    public async Task AnalyzerRejectsGenericHostBuildEntrypoints(string statement)
    {
        var diagnostic = Assert.Single(await GetAnalyzerDiagnosticsAsync(CreateModuleSource(statement)));

        Assert.Equal(AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Matches(
            "Build|Start|RunConsoleAsync",
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task AnalyzerRejectsCustomHostBuilderImplementation()
    {
        var source =
            """
            using System;
            using System.Collections.Generic;
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;

            namespace Sample.App;

            public sealed class CustomHostBuilder : IHostBuilder
            {
                public IDictionary<object, object> Properties { get; } =
                    new Dictionary<object, object>();

                public IHostBuilder ConfigureHostConfiguration(
                    Action<IConfigurationBuilder> configureDelegate) => this;

                public IHostBuilder ConfigureAppConfiguration(
                    Action<HostBuilderContext, IConfigurationBuilder> configureDelegate) => this;

                public IHostBuilder ConfigureServices(
                    Action<HostBuilderContext, IServiceCollection> configureDelegate) => this;

                public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
                    IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull => this;

                public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
                    Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
                    where TContainerBuilder : notnull => this;

                public IHostBuilder ConfigureContainer<TContainerBuilder>(
                    Action<HostBuilderContext, TContainerBuilder> configureDelegate) => this;

                public IHost Build() => throw new NotSupportedException();
            }

            public sealed class AppModule : ModuleBase
            {
                public override void ConfigureServices(ServiceConfigurationContext context)
                {
                    _ = new CustomHostBuilder().Build();
                }
            }
            """;

        var diagnostic = Assert.Single(await GetAnalyzerDiagnosticsAsync(source));

        Assert.Equal(AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed, diagnostic.Id);
    }

    [Fact]
    public async Task AnalyzerRejectsCustomServiceProviderFactoryImplementation()
    {
        var source =
            """
            using System;
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.DependencyInjection;

            namespace Sample.App;

            public sealed class CustomFactory : IServiceProviderFactory<IServiceCollection>
            {
                public IServiceCollection CreateBuilder(IServiceCollection services) => services;

                public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder) =>
                    throw new NotSupportedException();
            }

            public sealed class AppModule : ModuleBase
            {
                public override void ConfigureServices(ServiceConfigurationContext context)
                {
                    _ = new CustomFactory().CreateServiceProvider(context.Services);
                }
            }
            """;

        var diagnostic = Assert.Single(await GetAnalyzerDiagnosticsAsync(source));

        Assert.Equal(AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed, diagnostic.Id);
    }

    [Fact]
    public async Task AnalyzerRejectsProviderBuildInExternalSourceHelper()
    {
        var source =
            """
            using Microsoft.Extensions.DependencyInjection;

            namespace Sample.App;

            public static class ServiceRegistrationHelper
            {
                public static void Configure(IServiceCollection services)
                {
                    services.BuildServiceProvider();
                }
            }
            """;

        var diagnostic = Assert.Single(await GetAnalyzerDiagnosticsAsync(source));

        Assert.Equal(AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed, diagnostic.Id);
    }

    [Fact]
    public async Task AnalyzerRejectsGenericHostBuildInExternalSourceHelper()
    {
        var source =
            """
            using Microsoft.Extensions.Hosting;

            namespace Sample.App;

            public static class HostFactory
            {
                public static IHost Create()
                {
                    return Host.CreateDefaultBuilder().Build();
                }
            }
            """;

        var diagnostic = Assert.Single(await GetAnalyzerDiagnosticsAsync(source));

        Assert.Equal(AnalyzerDiagnosticIds.BuildServiceProviderNotAllowed, diagnostic.Id);
    }

    [Fact]
    public async Task AnalyzerAllowsNormalDependencyInjectionUsage()
    {
        var source =
            """
            using System;
            using AtomUI.City.Core.Hosting;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;

            namespace Sample.App;

            public static class ServiceRegistrationHelper
            {
                public static void Configure(
                    IServiceCollection services,
                    IServiceProvider provider,
                    IHost host)
                {
                    services.AddSingleton<object>();
                    IServiceProviderFactory<IServiceCollection> factory = new DefaultServiceProviderFactory();
                    _ = factory.CreateBuilder(services);
                    using var scope = provider.CreateScope();
                    _ = scope.ServiceProvider.GetRequiredService<object>();
                    _ = Host.CreateApplicationBuilder();
                    _ = ApplicationHost.CreateBuilder().Build();
                    _ = host.StartAsync();
                    CustomProviderFactory.BuildServiceProvider();
                    CustomProviderFactory.CreateServiceProvider();
                    CustomProviderFactory.Build();
                }
            }

            public static class CustomProviderFactory
            {
                public static void BuildServiceProvider()
                {
                }

                public static void CreateServiceProvider()
                {
                }

                public static void Build()
                {
                }
            }
            """;

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("((IServiceCollection)context.Services).BuildServiceProvider();")]
    [InlineData("_ = Host.CreateApplicationBuilder().Build();")]
    public async Task AnalyzerSkipsTestProjects(string statement)
    {
        var diagnostics = await GetAnalyzerDiagnosticsAsync(
            CreateModuleSource(statement),
            isTestProject: true);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AnalyzerSkipsGeneratedCode()
    {
        var source =
            """
            // <auto-generated/>
            using Microsoft.Extensions.DependencyInjection;

            namespace Sample.App;

            public static class GeneratedRegistration
            {
                public static void Configure(IServiceCollection services)
                {
                    services.BuildServiceProvider();
                }
            }
            """;

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    private static string CreateModuleSource(string statement)
    {
        return $$"""
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;

            namespace Sample.App;

            public sealed class AppModule : ModuleBase
            {
                public override void ConfigureServices(ServiceConfigurationContext context)
                {
                    {{statement}}
                }
            }
            """;
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        bool isTestProject = false)
    {
        var compilation = CreateCompilation(source);
        var compilationErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            compilationErrors.Length == 0,
            string.Join(Environment.NewLine, compilationErrors.Select(diagnostic => diagnostic.ToString())));

        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(isTestProject));

        return await compilation
            .WithAnalyzers(
                [new BuildServiceProviderUsageAnalyzer()],
                analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat([
                MetadataReference.CreateFromFile(typeof(ModuleBase).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ServiceCollection).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IHostBuilder).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(HostApplicationBuilder).Assembly.Location),
            ])
            .DistinctBy(reference => reference.Display)
            .ToArray();

        return CSharpCompilation.Create(
            "Sample.App",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions EmptyOptions =
            new TestAnalyzerConfigOptions(new Dictionary<string, string>());

        public TestAnalyzerConfigOptionsProvider(bool isTestProject)
        {
            GlobalOptions = new TestAnalyzerConfigOptions(new Dictionary<string, string>
            {
                ["build_property.IsTestProject"] = isTestProject.ToString(),
            });
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return EmptyOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return EmptyOptions;
        }
    }

    private sealed class TestAnalyzerConfigOptions(
        IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var configuredValue))
            {
                value = configuredValue;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
