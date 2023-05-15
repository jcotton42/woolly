using Microsoft.EntityFrameworkCore;

using Woolly.Data;
using Woolly.Data.Models;

namespace Woolly.Infrastructure;

/// <summary>
/// Provides short-lived, persistent state for commands.
/// </summary>
public sealed class StateService
{
    private readonly WoollyContext _db;

    public StateService(WoollyContext db) => _db = db;

    public async Task<int> AddAsync(string data, CancellationToken ct)
    {
        var state = new State { Data = data };
        _db.States.Add(state);
        await _db.SaveChangesAsync(ct);
        return state.Id;
    }

    public async Task<string> TakeAsync(int id, CancellationToken ct)
    {
        var state = await _db.States.FirstAsync(s => s.Id == id, ct);
        var data = state.Data;
        _db.States.Remove(state);
        await _db.SaveChangesAsync(ct);
        return data;
    }
}
