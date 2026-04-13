using backend.Auth;
using Xunit;

namespace SeeMusic.Backend.Tests;

public class JwtTokenProviderTests
{
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenProvider _tokenProvider;

    public JwtTokenProviderTests()
    {
        _jwtSettings = new JwtSettings
        {
            Secret = "this-is-a-test-secret-key-with-more-than-32-characters",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiresInMinutes = 120
        };
        _tokenProvider = new JwtTokenProvider(_jwtSettings);
    }

    [Fact]
    public void GenerateAccessToken_ShouldCreateValidToken()
    {
        // Arrange
        var userId = 1;
        var username = "testuser";

        // Act
        var token = _tokenProvider.GenerateAccessToken(userId, username);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldCreateRandomToken()
    {
        // Act
        var token1 = _tokenProvider.GenerateRefreshToken();
        var token2 = _tokenProvider.GenerateRefreshToken();

        // Assert
        Assert.NotNull(token1);
        Assert.NotNull(token2);
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void ValidateToken_WithValidToken_ShouldReturnPrincipal()
    {
        // Arrange
        var userId = 1;
        var username = "testuser";
        var token = _tokenProvider.GenerateAccessToken(userId, username);

        // Act
        var principal = _tokenProvider.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        Assert.NotNull(userIdClaim);
        Assert.Equal(userId.ToString(), userIdClaim.Value);
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ShouldReturnNull()
    {
        // Act
        var principal = _tokenProvider.ValidateToken("invalid-token");

        // Assert
        Assert.Null(principal);
    }
}
