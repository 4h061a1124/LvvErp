using System.Text;

namespace LvvErp.Infrastructure.Features.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "LvvErp";
    public string Audience { get; set; } = "LvvErp";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Never commit a production key.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 480;

    public byte[] SigningKeyBytes => Encoding.UTF8.GetBytes(SigningKey);
}
