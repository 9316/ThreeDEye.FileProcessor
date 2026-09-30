# ThreeDEye.FileProcessor

Two .NET 9 console applications:

- **Generator** — creates a test file of a requested size in the format `Number. Text`.
- **Sorter** — sorts that file by **Text ascending, then Number ascending**, using External Merge Sort so that memory usage stays bounded regardless of input size.

---

## Assignment Summary

The input file has lines of the form:

```
415. Apple
30432. Something something something
1. Apple
32. Cherry is the best
2. Banana is yellow
```

The sort key is the text first; when two lines share the same text, the smaller number comes first. Expected output for the sample above:

```
1. Apple
415. Apple
2. Banana is yellow
32. Cherry is the best
30432. Something something something
```

The file is expected to be far larger than available RAM, so it cannot be loaded into memory.

---

## Architecture Overview

Both applications use **Vertical Slice Architecture**: code is organized by feature, not by technical layer.

```
Features/SortFile/
	Configuration/   options, defaults, validation
	Contracts/       request model
	Handlers/        feature entry point
	Reading/         chunk reader
	Parsing/         record + parser
	Sorting/         comparer + chunk sorter
	Writing/         sorted run writer
	Merging/         k-way merger + multi-pass planner
	TemporaryFiles/  temp workspace lifetime
	Pipeline/        orchestration
	Validation/      request validation
```

### Vertical Slice Rationale

A layered structure (`Services/`, `Repositories/`, `Models/`) would scatter one feature across many folders and force shared, growing abstractions. With slices:

- Everything needed to understand "sort a file" lives in one folder.
- Changes stay local; there is no shared service class that every feature edits.
- Types are `internal` by default and only exposed to tests and benchmarks through `InternalsVisibleTo`, so there is no public API surface to maintain.

The subfolders inside the slice separate responsibilities (read / parse / sort / write / merge), which keeps each class small and independently testable without reintroducing global layers.

---

## Project Structure

```
ThreeDEye.FileProcessor.slnx
.editorconfig
.gitignore
.github/workflows/ci.yml

src/
  ThreeDEye.FileProcessor.Generator/
  ThreeDEye.FileProcessor.Sorter/

tests/
  UnitTests/
	ThreeDEye.FileProcessor.Generator.UnitTests/
	ThreeDEye.FileProcessor.Sorter.UnitTests/
  IntegrationTests/
	ThreeDEye.FileProcessor.Generator.IntegrationTests/
	ThreeDEye.FileProcessor.Sorter.IntegrationTests/

benchmarks/
  ThreeDEye.FileProcessor.Benchmarks/
```

---

## Generator Flow

1. Configuration is bound from `appsettings.json` (`Generator` section) and validated at startup.
2. `RandomRecordGenerator` builds a fixed-size pool of text values. Reusing a pool is deliberate: it guarantees **duplicate text values**, which is what makes the Number tie-break meaningful when sorting.
3. `FileGenerator` streams records through a buffered `StreamWriter`, tracking bytes written, until the target size is reached.
4. Output is written to a `.processing` file and then moved into place, so a partial file is never mistaken for a complete one.

Encoding is UTF-8 **without BOM**, so the generated file round-trips cleanly through the Sorter.

---

## File Format Contract

### Record separator

Every line is exactly `<Number>. <Text>` — the separator is a period followed by a
single space. It is defined once per assembly (`GeneratorDefaults.RecordSeparator`
and `FileRecord.Separator`) rather than repeated as a literal.

### Output encoding and line endings

All output — both the generated file and the sorted file — is written as **UTF-8
without a byte order mark**, with lines terminated by **`"\n"`**, never
`Environment.NewLine`. `FileGenerator` sets this explicitly: the `StreamWriter` is
constructed with `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` and its
`NewLine` property is assigned `"\n"`.

This is a correctness requirement, not a formatting preference. The Generator counts
the bytes it writes in order to stop at the requested target size, and the Sorter
sizes its chunks using the same accounting. A platform-dependent terminator would
make the produced file size differ between Windows and Linux and would break
deterministic chunk sizing. The test
`GenerateAsync_WritesCompleteRecordsOnlyWithNoTrailingPartialLine` asserts the `"\n"`
terminator, and `WriteChunkAsync_WritesUtf8WithoutByteOrderMark` asserts the BOM-free
output.

### Input encoding

The Sorter reads input as UTF-8 with **byte order mark detection enabled**. Input is
often produced by external tools that legitimately emit a preamble, so BOM-prefixed
files are a supported input format: the preamble is consumed by the reader and never
reaches the record parser. Files produced by this pipeline never contain one, so
round-tripping remains exact. Covered by
`BomPrefixedInput_ShouldBeSortedAndOutputWrittenWithoutBom`.

---

## Sorter Flow

### External Merge Sort Rationale

The file does not fit in memory, so an in-memory sort is not an option. External Merge Sort is the standard solution:

1. **Split** the input into chunks that do fit in memory.
2. **Sort** each chunk in memory and write it to disk as a sorted *run*.
3. **Merge** the sorted runs into one sorted output using a k-way merge.

Peak memory is therefore determined by chunk size and worker count — not by input size.

### Bounded Memory Strategy

Memory is bounded by three independent limits:

| Limit | Control |
|---|---|
| Records held per chunk | `ChunkSizeMb` |
| Chunks in flight at once | `ChannelCapacity` + `MaxDegreeOfParallelism` |
| Files open during a merge | `MergeFanIn` |

Nothing in the pipeline reads the whole file into memory. Reading, writing and merging are all streaming operations over one line at a time.

### Producer/Consumer Pipeline

`ExternalFileSorter` runs a single producer and N consumers connected by a `Channel<List<FileRecord>>`:

- **Producer** — `ChunkReader` streams the input and yields chunks once the configured byte budget is reached.
- **Consumers** — each takes a chunk, sorts it in memory with `FileRecordComparer`, and writes it out as a sorted run.

Reading is I/O-bound and sorting is CPU-bound, so overlapping them keeps the disk busy while the CPU sorts.

### Backpressure

The channel is created with `BoundedChannelFullMode.Wait` and a capacity of `ChannelCapacity`. When consumers fall behind, `WriteAsync` suspends the producer instead of buffering. This is what prevents an unbounded read-ahead from defeating the memory bound — the reader can never get more than `ChannelCapacity` chunks ahead of the sorters.

### Controlled Concurrency

`MaxDegreeOfParallelism` fixes the number of sorting workers. It is a configured value rather than "as many as possible" because each worker holds a full chunk in memory; peak memory is roughly `ChunkSizeMb × (MaxDegreeOfParallelism + ChannelCapacity)`. Run indices are allocated under a lock so run filenames never collide.

### K-way Merge

`ChunkMerger` opens all runs in a group simultaneously and uses a `PriorityQueue` keyed by `FileRecordComparer`. It repeatedly takes the smallest head record, writes it, and pulls the next line from that same run. Only one record per open run is in memory at a time, so a k-way merge of any size runs in O(k) memory.

A k-way merge is used instead of repeated 2-way merges because it reads each record once per pass rather than once per merge level.

### Multi-pass Merge

Merging thousands of runs at once would exhaust file handles and thrash the disk, so `MergePassPlanner` splits runs into groups of at most `MergeFanIn`. Each pass merges its groups and produces a smaller set of runs; passes repeat until one run remains. Intermediate files are deleted at the end of each pass, so temp disk usage does not grow across passes.

Single-item groups are passed through unchanged rather than copied.

### Atomic Output

The final merge writes to `<output>.processing` and then does `File.Move(..., overwrite: true)`. A move within a volume is atomic, so the output path either does not exist or contains a complete sorted file. A crash mid-merge never leaves a truncated file at the real output path.

### Cancellation

A `CancellationToken` is threaded through the reader, the channel, the workers, and every merge pass. `Program` wires it to `Console.CancelKeyPress`, so Ctrl+C stops work promptly. Because the token is passed into the async file APIs, cancellation is observed during I/O and not just between chunks.

### Temp-file Cleanup

`TemporaryFileManager` creates a unique workspace directory per run and implements `IDisposable`. It is held in a `using`, so the entire workspace — including runs left behind by a failed or cancelled operation — is removed when the sort ends, regardless of outcome.

### Error Handling

- Input path missing, or input equal to output, fails fast with a clear exception before any work starts.
- Malformed lines raise `InvalidDataException` including the line number and the offending content, rather than being silently skipped. A corrupt input is a real problem and should not produce a silently-wrong output.
- Configuration errors are caught at startup by `ValidateOnStart` and reported as a list, so all problems surface at once.
- Cleanup happens in `finally` / `Dispose`, so failures do not leak temp files.

---

## Configuration

Settings come from `appsettings.json`, can be overridden by `THREEDEYE_`-prefixed environment variables, and finally by command-line arguments. All values are validated at startup.

**Generator** (`appsettings.json`, section `Generator`):

| Setting | Default | Meaning |
|---|---|---|
| `OutputPath` | `data/generated.txt` | Generated file path |
| `TargetFileSizeBytes` | `10485760` | Target size in bytes |
| `BufferSizeKb` | `32` | Write buffer size |
| `StringPoolSize` | `100` | Distinct text values (drives duplicates) |
| `StartingNumber` | `1` | Lowest generated number |

**Sorter** (`appsettings.json`, section `Sorter`):

| Setting | Default | Meaning |
|---|---|---|
| `InputPath` | `data/generated.txt` | File to sort (matches the Generator default) |
| `OutputPath` | `data/output.txt` | Sorted result |
| `TemporaryDirectory` | `null` | Temp root; system temp when unset |
| `ChunkSizeMb` | `10` | In-memory chunk budget |
| `BufferSizeKb` | `32` | I/O buffer size |
| `MaxDegreeOfParallelism` | `4` | Sorting workers |
| `ChannelCapacity` | `4` | Backpressure bound |
| `MergeFanIn` | `16` | Runs merged per group |

The algorithm and the ordering rule are fixed by the assignment and are intentionally **not** configurable.

---

## How to Run

### Generator

```bash
dotnet run --project src/ThreeDEye.FileProcessor.Generator -c Release
```

Override the output and size (1 GB shown):

```bash
dotnet run --project src/ThreeDEye.FileProcessor.Generator -c Release -- \
  --Generator:OutputPath="data/input.txt" \
  --Generator:TargetFileSizeBytes=1073741824
```

### Sorter

```bash
dotnet run --project src/ThreeDEye.FileProcessor.Sorter -c Release
```

With explicit tuning:

```bash
dotnet run --project src/ThreeDEye.FileProcessor.Sorter -c Release -- \
  --Sorter:InputPath="data/input.txt" \
  --Sorter:OutputPath="data/output.txt" \
  --Sorter:ChunkSizeMb=64 \
  --Sorter:BufferSizeKb=64 \
  --Sorter:MaxDegreeOfParallelism=8 \
  --Sorter:ChannelCapacity=8 \
  --Sorter:MergeFanIn=16
```

### Unit Tests

```bash
dotnet test tests/UnitTests/ThreeDEye.FileProcessor.Sorter.UnitTests -c Release
dotnet test tests/UnitTests/ThreeDEye.FileProcessor.Generator.UnitTests -c Release
```

### Integration Tests

```bash
dotnet test tests/IntegrationTests/ThreeDEye.FileProcessor.Sorter.IntegrationTests -c Release
dotnet test tests/IntegrationTests/ThreeDEye.FileProcessor.Generator.IntegrationTests -c Release
```

### Everything

```bash
dotnet test ThreeDEye.FileProcessor.slnx -c Release
```

### Benchmarks

Micro-benchmarks (BenchmarkDotNet):

```bash
dotnet run --project benchmarks/ThreeDEye.FileProcessor.Benchmarks -c Release -- --filter *
```

End-to-end run against a real file. Arguments are
`e2e <input> <output> [chunkMb] [bufferKb] [workers] [channelCapacity] [mergeFanIn]`:

```bash
dotnet run --project benchmarks/ThreeDEye.FileProcessor.Benchmarks -c Release -- \
  e2e ./perf/input.txt ./perf/output.txt 64 64 8 8 16
```

This prints input size, elapsed time, throughput, the effective configuration, peak working set, and runtime information.

---

## Performance Validation

Every number in this section was measured on the machine described below. Nothing is
extrapolated.

**Scope statement**

- The implementation is **designed to support approximately 100 GB input files using
  bounded external sorting.**
- **100 GB was not empirically validated in this environment because available disk space
  was insufficient for a safe external-sort run. The largest successfully validated input
  was 10 GB.**

An external merge sort transiently needs room for the input, the sorted runs, and the
output at the same time. For this implementation and this validation setup that worked out
to roughly three times the input size; that is a conservative capacity estimate for these
runs, not a universal guarantee — the real figure depends on fan-in, pass count, and when
intermediate files are released. With 141.5 GB free, a 100 GB run had no safe margin, so
it was not attempted rather than reported as untested-but-assumed.

**Environment**

| | |
|---|---|
| CPU | Intel Core i7-10750H @ 2.60 GHz, 6 cores / 12 logical |
| RAM | 15.8 GB |
| Storage | SSD, 475.8 GB total / 141.5 GB free at start |
| OS | Windows 10.0.19045 |
| Runtime | .NET 9 |
| Build | Release |

### End-to-end runs

All three runs used `BufferSizeKb=64`, `MaxDegreeOfParallelism=4`, `ChannelCapacity=4`,
`MergeFanIn=16`, `ChunkSizeMb=64`.

| Input | Input bytes | Output bytes | Records | Gen time | Sort time | Verify time | Sort throughput | Peak working set | Initial runs | Merge passes |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 GB | 1,073,741,826 | 1,073,741,826 | 37,538,181 | 5.06 s | 77.07 s | 6.05 s | 13.29 MB/s | 1,357 MB | 16 | 1 |
| 5 GB | 5,368,709,125 | 5,368,709,125 | 183,271,644 | 21.20 s | 424.18 s | 30.53 s | 12.07 MB/s | 1,834 MB | 80 | 2 |
| 10 GB | 10,737,418,248 | 10,737,418,248 | 362,827,033 | 41.37 s | 861.69 s | 59.72 s | 11.88 MB/s | 1,902 MB | 160 | 2 |

Input and output byte counts are identical at every size: sorting only reorders records, so
any difference would indicate a format or line-terminator defect.

### Correctness verification

Output was validated with a streaming, bounded-memory verifier (`perf/Verifier`, not part of
the solution) that holds only the previous record plus fixed-size accumulators. It checks
that every line parses, that the file is globally ordered by Text (ordinal) then Number
ascending, and it computes an **order-independent fingerprint** — a sum of per-line FNV-1a
hashes. Because addition is commutative, the same fingerprint over the unsorted input and the
sorted output proves no record was lost, duplicated, or altered.

| Input | Records in / out | Fingerprint in / out | Globally ordered |
|---|---|---|---|
| 1 GB | 37,538,181 / 37,538,181 | 18340647070696217046 (identical) | yes |
| 5 GB | 183,271,644 / 183,271,644 | 11230308645709116689 (identical) | yes |
| 10 GB | 362,827,033 / 362,827,033 | 167365868164343658 (identical) | yes |

The verifier's own peak working set stayed at 54–58 MB across all three sizes, confirming the
check itself is bounded-memory.

### Memory scaling

| Input | Peak working set | Growth vs 1 GB |
|---|---|---|
| 1 GB | 1,357 MB | 1.00x |
| 5 GB | 1,834 MB | 1.35x |
| 10 GB | 1,902 MB | 1.40x |

A 10x increase in input produced a 1.4x increase in peak memory, and the curve flattens
between 5 GB and 10 GB. Memory is therefore governed by configuration, not input size.

This is a measured trend, not a proven hard ceiling. Peak working set is well above the raw
`ChunkSizeMb x (workers + channel capacity)` budget because each record is a managed
`FileRecord` holding a separate `string` object — object headers, references, and UTF-16
storage cost far more than the raw UTF-8 bytes — and because the GC does not return memory
to the OS eagerly. The three configuration levers behave as designed: `ChunkSizeMb` sets how
much data one worker holds, `MaxDegreeOfParallelism` multiplies that by the number of
concurrent sorts, and `ChannelCapacity` caps how far the reader may run ahead. The Generator,
which streams without buffering chunks, stayed flat at ~43 MB for both 5 GB and 10 GB.

### Multi-pass merge validation

A dedicated run forced many passes by combining a small chunk size with a small fan-in.

| Setting | Value |
|---|---|
| Input | 268,435,468 bytes (256 MB), 9,661,173 records |
| ChunkSizeMb | 8 |
| MergeFanIn | 2 |
| Initial runs | 32 |
| Merge passes | 5 (32 -> 16 -> 8 -> 4 -> 2 -> 1) |
| Output bytes | 268,435,468 (byte parity) |
| Records out | 9,661,173 |
| Fingerprint | 2984882540722756533 (identical to input) |
| Globally ordered | yes |
| Temp workspace after run | empty |

The 5 GB and 10 GB production-configuration runs also required 2 merge passes each, so the
multi-pass path is exercised under normal settings and not only under forced ones.

### Micro-benchmarks (BenchmarkDotNet, ShortRun)

| Benchmark | N | Mean | Allocated |
|---|---|---|---|
| `FileRecordParserBenchmarks.ParseLines` | 10,000 lines | 602.1 us | 887.5 KB |
| `FileRecordComparerBenchmarks.SortChunk` | 100,000 records | 38.58 ms | 0 B |

Parsing runs at ~16.6 M lines/s single-threaded, which accounts for roughly 22 s of the 861 s
10 GB run (~2.5%). Comparison work is about 140 s of CPU spread across 4 workers. The 10 GB
two-pass sort moves roughly 60 GB through the disk in 861 s (~71 MB/s), so **the pipeline is
I/O-bound, not CPU-bound** — which is why no CPU-side micro-optimization was adopted.

---

## Trade-offs

- **Correctness over raw speed.** Malformed input throws instead of being skipped. A sort that silently drops lines is worse than one that fails.
- **`string` records instead of byte spans.** Working on `ReadOnlySpan<byte>` and comparing raw UTF-8 would cut allocations substantially, but string-based records are far easier to read and test. This is the single biggest performance item left on the table.
- **Fixed worker count instead of dynamic scaling.** Predictable memory is worth more here than adaptive throughput, since the whole point is to survive inputs bigger than RAM.
- **Multi-pass merge instead of a single huge merge.** Costs extra I/O passes for very large run counts, but avoids file-handle exhaustion and keeps behaviour predictable.
- **Ordinal comparison.** `StringComparison.Ordinal` is used throughout — it is fast and deterministic, but it is not culture-aware alphabetical ordering. The assignment does not require culture-aware sorting.
- **Sequential merge passes.** Merge groups within a pass are merged one at a time. Simpler to reason about, but leaves parallelism unused during the merge stage.

---

## Future Optimizations

Ordered roughly by expected value. The measured 10 GB run is I/O-bound (~71 MB/s of disk traffic against ~2.5% of wall-clock spent parsing), so none of these are currently justified by the numbers.

1. **Parallelize merge groups within a pass.** The merge stage is sequential while chunking is parallel. This is the most plausible remaining win, but only if the disk is not already saturated.
2. **Reduce the merge pass count.** The 10 GB run used 2 passes; a larger `MergeFanIn` would make it 1 and remove a full read+write of the dataset. This is a configuration change, not a code change.
3. **Byte-level parsing and comparison.** Avoid decoding to `string` and compare UTF-8 directly. This would cut the 887.5 KB per 10,000 lines the parser allocates, but parsing is only ~2.5% of the measured run.
4. **Pool the chunk buffers.** Reuse `List<FileRecord>` instances across chunks to reduce GC pressure at high worker counts.
5. **Async/overlapped I/O during merge.** Prefetch the next lines of each run so the priority queue is never waiting on disk.
6. **Validate at 100 GB.** Requires a machine with substantially more free disk than the 141.5 GB available here.
