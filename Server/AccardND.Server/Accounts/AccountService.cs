using System.Security.Cryptography;
using System.Text.Json;

namespace AccardND.Server.Accounts;

public sealed record AccountIdentity(string PlayerId, string Username);

public sealed class AccountService
{
    private sealed record StoredAccount(string PlayerId, string Username, string Salt, string Hash);

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Pbkdf2Iterations = 100_000;

    private readonly object gate = new();
    private readonly Dictionary<string, StoredAccount> accountsByUsername =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AccountIdentity> identitiesByToken = new();
    private readonly string filePath;

    public AccountService(ServerConfig config)
    {
        filePath = config.AccountsFilePath;
        LoadFromDisk();
    }

    public (AccountIdentity Identity, string Token, string Error) Register(string username, string password)
    {
        username = username?.Trim() ?? string.Empty;
        if (username.Length is < 3 or > 20)
            return (null, null, "Il nome utente deve avere 3-20 caratteri.");
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            return (null, null, "La password deve avere almeno 6 caratteri.");

        lock (gate)
        {
            if (accountsByUsername.ContainsKey(username))
                return (null, null, "Nome utente già in uso.");

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = HashPassword(password, salt);
            var account = new StoredAccount(
                Guid.NewGuid().ToString("N"),
                username,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
            accountsByUsername[username] = account;
            SaveToDisk();
            return IssueSession(account);
        }
    }

    public (AccountIdentity Identity, string Token, string Error) Login(string username, string password)
    {
        username = username?.Trim() ?? string.Empty;
        lock (gate)
        {
            if (!accountsByUsername.TryGetValue(username, out StoredAccount account))
                return (null, null, "Credenziali non valide.");

            byte[] salt = Convert.FromBase64String(account.Salt);
            byte[] expected = Convert.FromBase64String(account.Hash);
            byte[] actual = HashPassword(password ?? string.Empty, salt);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                return (null, null, "Credenziali non valide.");

            return IssueSession(account);
        }
    }

    public AccountIdentity ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        lock (gate)
            return identitiesByToken.GetValueOrDefault(token);
    }

    private (AccountIdentity, string, string) IssueSession(StoredAccount account)
    {
        var identity = new AccountIdentity(account.PlayerId, account.Username);
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        identitiesByToken[token] = identity;
        return (identity, token, null);
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);

    private void LoadFromDisk()
    {
        if (!File.Exists(filePath))
            return;
        var stored = JsonSerializer.Deserialize<List<StoredAccount>>(File.ReadAllText(filePath));
        if (stored == null)
            return;
        foreach (StoredAccount account in stored)
            accountsByUsername[account.Username] = account;
    }

    private void SaveToDisk()
    {
        var snapshot = accountsByUsername.Values.ToList();
        File.WriteAllText(filePath, JsonSerializer.Serialize(
            snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }
}
