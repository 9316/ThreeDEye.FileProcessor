using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;

/// <summary>
/// Validates <see cref="SortFileOptions"/> at startup so misconfiguration fails fast
/// rather than part-way through a long-running sort.
/// </summary>
internal sealed class SortFileOptionsValidator : IValidateOptions<SortFileOptions>
{
    public ValidateOptionsResult Validate(string? name, SortFileOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Sorter options are missing.");
        }

        var failures = new List<string>();
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true))
        {
            failures.AddRange(results.Select(r => r.ErrorMessage ?? "Invalid Sorter option."));
        }

        if (!string.IsNullOrWhiteSpace(options.InputPath) &&
            !string.IsNullOrWhiteSpace(options.OutputPath) &&
            string.Equals(
                Path.GetFullPath(options.InputPath),
                Path.GetFullPath(options.OutputPath),
                StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Sorter:InputPath and Sorter:OutputPath must differ.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
