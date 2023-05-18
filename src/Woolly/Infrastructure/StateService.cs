using Microsoft.EntityFrameworkCore;

using NodaTime;

using Remora.Results;

using Woolly.Data;
using Woolly.Data.Models;

namespace Woolly.Infrastructure;

/// <summary>
/// Provides short-lived, persistent state for commands.
/// </summary>
public sealed class StateService
{
    private readonly IClock _clock;
    private readonly WoollyContext _db;

    public StateService(IClock clock, WoollyContext db)
    {
        _clock = clock;
        _db = db;
    }

    public async Task<int> AddAsync(string data, Duration lifetime, CancellationToken ct)
    {
        if (lifetime <= Duration.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be greater than zero.");
        }

        var state = new State { Data = data, ExpiryTime = _clock.GetCurrentInstant() + lifetime };
        _db.States.Add(state);
        await _db.SaveChangesAsync(ct);
        return state.Id;
    }

    public async Task<Result<string>> TryTakeAsync(int id, CancellationToken ct)
    {
        var state = await _db.States.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (state is null) return new NotFoundError();
        var data = state.Data;
        _db.States.Remove(state);
        await _db.SaveChangesAsync(ct);
        return data;
    }
}
