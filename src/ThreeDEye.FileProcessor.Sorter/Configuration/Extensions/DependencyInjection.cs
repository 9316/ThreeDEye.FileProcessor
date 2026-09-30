using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Handlers;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Merging;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Pipeline;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Reading;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Writing;

namespace ThreeDEye.FileProcessor.Sorter.Configuration.Extensions;

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

        services.AddOptions<SortFileOptions>()
            .Bind(configuration.GetSection(SortFileOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SortFileOptions>, SortFileOptionsValidator>();

        // These collaborators hold no per-run mutable state, so a single shared instance is
        // safe and avoids re-allocating them for every sort.
        services.AddSingleton<ChunkReader>();
        services.AddSingleton<ChunkSorter>();
        services.AddSingleton<SortedChunkWriter>();
        services.AddSingleton<ChunkMerger>();
        services.AddSingleton<MergePassPlanner>();

        services.AddSingleton<ExternalFileSorter>();
        services.AddSingleton<SortFileHandler>();

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
