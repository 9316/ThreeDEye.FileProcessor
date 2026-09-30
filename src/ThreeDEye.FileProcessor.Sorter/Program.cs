using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThreeDEye.FileProcessor.Sorter.Configuration.Extensions;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Contracts;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Handlers;

namespace ThreeDEye.FileProcessor.Sorter;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var configuration = DependencyInjection.BuildConfiguration(args);
            await using var provider = (ServiceProvider)DependencyInjection.BuildServiceProvider(configuration);

            // Resolve the configuration once for this run; later reloads affect the next run only.
            var options = provider.GetRequiredService<IOptionsMonitor<SortFileOptions>>().CurrentValue;
            var handler = provider.GetRequiredService<SortFileHandler>();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            await handler.HandleAsync(SortFileRequest.FromOptions(options), options, cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation canceled.");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
