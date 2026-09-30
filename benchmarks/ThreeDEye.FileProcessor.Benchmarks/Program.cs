using System.Diagnostics;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Running;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Contracts;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Handlers;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Pipeline;

namespace ThreeDEye.FileProcessor.Benchmarks;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // "e2e <input> <output> [chunkMb] [bufferKb] [workers] [channel] [fanIn]"
        // measures a real sort of a real file. Everything else runs BenchmarkDotNet.
        if (args.Length > 0 && string.Equals(args[0], "e2e", StringComparison.OrdinalIgnoreCase))
        {
            return await RunEndToEndAsync(args.AsSpan(1).ToArray());
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }

    private static async Task<int> RunEndToEndAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: e2e <input> <output> [chunkMb] [bufferKb] [workers] [channel] [fanIn]");
            return 1;
        }

        var options = new SortFileOptions
        {
            InputPath = args[0],
            OutputPath = args[1],
            ChunkSizeMb = args.Length > 2 ? int.Parse(args[2]) : 64,
            BufferSizeKb = args.Length > 3 ? int.Parse(args[3]) : 64,
            MaxDegreeOfParallelism = args.Length > 4 ? int.Parse(args[4]) : Environment.ProcessorCount,
            ChannelCapacity = args.Length > 5 ? int.Parse(args[5]) : 8,
            MergeFanIn = args.Length > 6 ? int.Parse(args[6]) : 16
        };

        var inputBytes = new FileInfo(options.InputPath).Length;
        var handler = new SortFileHandler(new ExternalFileSorter());

        // Measure only the sort itself.
        var sw = Stopwatch.StartNew();
        await handler.HandleAsync(SortFileRequest.FromOptions(options), options, CancellationToken.None);
        sw.Stop();

        var mb = inputBytes / 1024d / 1024d;
        Console.WriteLine($"InputBytes            : {inputBytes:N0} ({mb:N2} MB)");
        Console.WriteLine($"Elapsed               : {sw.Elapsed.TotalSeconds:N2} s");
        Console.WriteLine($"Throughput            : {mb / sw.Elapsed.TotalSeconds:N2} MB/s");
        Console.WriteLine($"ChunkSizeMb           : {options.ChunkSizeMb}");
        Console.WriteLine($"BufferSizeKb          : {options.BufferSizeKb}");
        Console.WriteLine($"MaxDegreeOfParallelism: {options.MaxDegreeOfParallelism}");
        Console.WriteLine($"ChannelCapacity       : {options.ChannelCapacity}");
        Console.WriteLine($"MergeFanIn            : {options.MergeFanIn}");
        Console.WriteLine($"PeakWorkingSet        : {Process.GetCurrentProcess().PeakWorkingSet64 / 1024d / 1024d:N2} MB");
        Console.WriteLine($"Runtime               : {Environment.Version} / {RuntimeInformation.OSDescription}");
        Console.WriteLine($"ProcessorCount        : {Environment.ProcessorCount}");
        return 0;
    }
}
