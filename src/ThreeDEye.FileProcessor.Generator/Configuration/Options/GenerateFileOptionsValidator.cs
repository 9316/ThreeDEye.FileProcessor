using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ThreeDEye.FileProcessor.Generator.Configuration.Options;

/// <summary>
/// Validates <see cref="GenerateFileOptions"/> at startup so misconfiguration fails fast
/// rather than part-way through a generation run.
/// </summary>
internal sealed class GenerateFileOptionsValidator : IValidateOptions<GenerateFileOptions>
{
    public ValidateOptionsResult Validate(string? name, GenerateFileOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Generator options are missing.");
        }

        var failures = new List<string>();
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true))
        {
            failures.AddRange(results.Select(r => r.ErrorMessage ?? "Invalid Generator option."));
        }

        if (options.StartingNumber > long.MaxValue - 1)
        {
            failures.Add("Generator:StartingNumber is too large to produce a sequential record numbering.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
