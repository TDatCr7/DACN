using System.Collections.Concurrent;

namespace CinemaS.Services
{
    public record RegisterOtpState(
        string Email,
        string Code,
        DateTimeOffset ExpireAt,
        bool Verified,
        string FullName
    );

    public interface IRegisterOtpStore
    {
        void SaveOtp(string email, string code, DateTimeOffset expireAt);
        bool TryGet(string email, out RegisterOtpState state);
        void MarkVerified(string email, string fullName);
        void Remove(string email);
    }

    public class RegisterOtpStore : IRegisterOtpStore
    {
        private readonly ConcurrentDictionary<string, RegisterOtpState> _store = new();

        private static string Key(string email) => (email ?? "").Trim().ToLowerInvariant();

        public void SaveOtp(string email, string code, DateTimeOffset expireAt)
        {
            var key = Key(email);
            _store[key] = new RegisterOtpState(key, code, expireAt, false, "");
        }

        public bool TryGet(string email, out RegisterOtpState state)
        {
            return _store.TryGetValue(Key(email), out state!);
        }

        public void MarkVerified(string email, string fullName)
        {
            var key = Key(email);
            if (_store.TryGetValue(key, out var s))
                _store[key] = s with { Verified = true, FullName = fullName ?? "" };
        }

        public void Remove(string email)
        {
            _store.TryRemove(Key(email), out _);
        }
    }
}
