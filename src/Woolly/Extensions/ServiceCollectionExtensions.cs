using System.Reflection;

using Microsoft.Extensions.Options;

using Remora.Discord.Interactivity;
using Remora.Discord.Interactivity.Extensions;

namespace Woolly.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddValidatedOptions<TOptions, TValidator>(this IServiceCollection services,
        string sectionName)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
        => services
            .AddSingleton<IValidateOptions<TOptions>, TValidator>()
            .AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateOnStart()
            .Services;

    public static IServiceCollection AddInteractionGroupsFromAssembly(this IServiceCollection services,
        Assembly assembly)
    {
        var candidates = assembly.GetTypes().Where(
            t => t is { IsClass: true, IsAbstract: false }
                 && t.IsAssignableTo(typeof(InteractionGroup))
        );

        foreach (var candidate in candidates)
        {
            services.AddInteractiveEntity(candidate);
        }

        return services;
    }
}
