using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreeDEye.FileProcessor.Generator.Configuration.Options;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Handlers;

namespace ThreeDEye.FileProcessor.Generator.Configuration.Extensions;

internal static class DependencyInjection
{
    public static IConfigurationRoot BuildConfiguration(string[] args) =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "THREEDEYE_")
            .AddCommandLine(args)
            .Build();

    public static IServiceProvider BuildServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSimpleConsole());

        services.AddOptions<GenerateFileOptions>()
            .Bind(configuration.GetSection(GenerateFileOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<GenerateFileOptions>, GenerateFileOptionsValidator>();

        // The record generator's pool is a startup-time decision, so it is built from the
        // current configuration snapshot. Reloads apply to the next execution.
        services.AddSingleton(sp =>
            new RandomRecordGenerator(sp.GetRequiredService<IOptionsMonitor<GenerateFileOptions>>().CurrentValue.StringPoolSize));

        services.AddSingleton<FileGenerator>();
        services.AddSingleton<GenerateFileHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // ValidateOnStart is executed by the hosting startup validator; these console apps do
        // not run a host, so the validator is invoked explicitly to keep fail-fast behaviour.
        provider.GetRequiredService<IStartupValidator>().Validate();

        return provider;
    }
}
