using System.Numerics;
using System.Security.Cryptography;

namespace RecServer.Photon;

/// <summary>
/// Server side of Photon's payload-encryption key exchange: Diffie-Hellman over the 768-bit Oakley group (RFC 2409 group 1)
/// with generator 22; both public keys and the shared secret travel as minimal big-endian unsigned bytes. The AES-256-CBC key is
/// SHA-256 of the shared secret, the IV is all zero, padding PKCS7.
/// </summary>
public sealed class PhotonCrypto
{
    private static readonly BigInteger Prime = new(Convert.FromHexString(
        "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DD" +
        "EF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A63A3620FFFFFFFFFFFFFFFF"), isUnsigned: true, isBigEndian: true);
    private static readonly BigInteger Generator = 22;

    private readonly Aes _aes;
    public byte[] ServerPublicKey { get; }

    public PhotonCrypto(byte[] clientPublicKey)
    {
        BigInteger secret;
        do secret = new BigInteger(RandomNumberGenerator.GetBytes(20), isUnsigned: true, isBigEndian: true);   // 160 bits, as the client
        while (secret.IsZero || secret >= Prime - 1);
        ServerPublicKey = Bytes(BigInteger.ModPow(Generator, secret, Prime));
        var shared = BigInteger.ModPow(new BigInteger(clientPublicKey, isUnsigned: true, isBigEndian: true), secret, Prime);
        _aes = Aes.Create();
        _aes.Key = SHA256.HashData(Bytes(shared));
        _aes.IV = new byte[16];
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.PKCS7;
    }

    private static byte[] Bytes(BigInteger v) => v.IsZero ? new byte[1] : v.ToByteArray(isUnsigned: true, isBigEndian: true);

    public byte[] Decrypt(byte[] data, int offset, int count) => _aes.DecryptCbc(data.AsSpan(offset, count), _aes.IV, PaddingMode.PKCS7);
    public byte[] Encrypt(byte[] data) => _aes.EncryptCbc(data, _aes.IV, PaddingMode.PKCS7);
}
