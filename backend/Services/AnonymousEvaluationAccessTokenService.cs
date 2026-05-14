using System.Security.Cryptography;
using System.Text;

namespace backend.Services;

public sealed class AnonymousEvaluationAccessTokenService : IAnonymousEvaluationAccessTokenService
{
    public string GenerateToken()
    {
        Span<byte> buffer = stackalloc byte[24];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    public bool ValidateToken(string expectedHash, string providedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedHash) || string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        var providedHash = HashToken(providedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        var providedBytes = Encoding.UTF8.GetBytes(providedHash);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}

public sealed class EvaluationTaskQueue : IEvaluationTaskQueue
{
    private readonly System.Threading.Channels.Channel<int> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<int>();

    public ValueTask QueueAsync(int evaluationDbId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(evaluationDbId, cancellationToken);
    }

    public ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
