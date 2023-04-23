using FluentValidation;

using Microsoft.Extensions.Options;

namespace Woolly.Infrastructure;

public abstract class AbstractOptionsValidator<T> : AbstractValidator<T>, IValidateOptions<T> where T : class
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        var result = Validate(options);
        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(e => e.ToString()));
    }
}
