using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using StackPilot.Application.Interfaces;

namespace StackPilot.Infrastructure.Security;

public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly byte[] _masterKey;

    public CredentialEncryptionService(IConfiguration configuration)
    {
        var key = configuration["StackPilot:EncryptionKey"] ?? "stackpilot-dev-key-change-in-production-32b";
        _masterKey = SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }

    public byte[] Encrypt(string plaintext, Guid organizationId)
    {
        var key = DeriveKey(organizationId);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    public string Decrypt(byte[] ciphertext, Guid organizationId)
    {
        var key = DeriveKey(organizationId);
        var nonce = ciphertext[..12];
        var tag = ciphertext[12..28];
        var encrypted = ciphertext[28..];
        var plaintext = new byte[encrypted.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, encrypted, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] DeriveKey(Guid organizationId)
    {
        var orgBytes = organizationId.ToByteArray();
        var combined = new byte[_masterKey.Length + orgBytes.Length];
        Buffer.BlockCopy(_masterKey, 0, combined, 0, _masterKey.Length);
        Buffer.BlockCopy(orgBytes, 0, combined, _masterKey.Length, orgBytes.Length);
        return SHA256.HashData(combined);
    }
}
