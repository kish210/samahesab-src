using System.Collections.Concurrent;

namespace SamaHesab.API.Services;

/// <summary>
/// In-memory refresh-token store with rotation. For a single-instance deployment this
/// is sufficient; a multi-instance deployment should back this with a Sec.RefreshTokens
/// table or a distributed cache (the interface stays the same).
/// </summary>
public class RefreshTokenStore
{
    private record Entry(AuthenticatedUser User, DateTime ExpiresAt);
    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public void Save(string refreshToken, AuthenticatedUser user, DateTime expiresAt)
        => _store[refreshToken] = new Entry(user, expiresAt);

    /// <summary>Validate + consume a refresh token (rotation: it is removed on use).</summary>
    public bool TryConsume(string refreshToken, out AuthenticatedUser? user)
    {
        user = null;
        if (!_store.TryRemove(refreshToken, out var entry)) return false;
        if (entry.ExpiresAt < DateTime.UtcNow) return false;
        user = entry.User;
        return true;
    }

    public void Revoke(string refreshToken) => _store.TryRemove(refreshToken, out _);
}
