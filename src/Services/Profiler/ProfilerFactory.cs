using Microsoft.Extensions.DependencyInjection;

namespace Octobot.Services.Profiler;

/// <summary>
/// Provides a method to create a <see cref="Profiler"/>. Useful in singletons.
/// </summary>
public sealed class ProfilerFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProfilerFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Creates a new <see cref="Profiler"/>.
    /// </summary>
    /// <returns>A new <see cref="Profiler"/>.</returns>
    // TODO: remove in future profiler PRs
    // ReSharper disable once UnusedMember.Global
    public Profiler Create()
    {
        return _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<Profiler>();
    }
}
