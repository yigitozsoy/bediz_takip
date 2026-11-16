using System.Numerics;
using System.Security.Cryptography;

namespace BambuBar.Core.Updates;

/// <summary>
/// beta: Ed25519 (RFC 8032, pure, not prehashed) over a stream, in plain <see cref="BigInteger"/> arithmetic. .NET has
/// no Ed25519; Bambu Bar shipped BouncyCastle (12 MB) for this one check. Verifying is all the app does, once per
/// update; signing runs in the release workflow. Neither is constant-time: verifying has no secret, and the signer
/// runs on a build machine where timing can't be observed.
/// </summary>
public static class Ed25519
{
    public const int KeySize = 32;
    public const int SignatureSize = 64;

    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    private static readonly BigInteger L = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    private static readonly BigInteger D = Mod(-121665 * Inverse(121666));
    private static readonly BigInteger D2 = Mod(2 * D);
    private static readonly BigInteger SqrtMinusOne = BigInteger.ModPow(2, (P - 1) / 4, P);
    private static readonly Point B = Decode(Base(), out _) ?? throw new InvalidOperationException("Ed25519 base point");
    private static readonly Point Zero = new(0, 1, 1, 0);

    /// <summary>A point in extended coordinates: x = X/Z, y = Y/Z, xy = T/Z.</summary>
    private readonly record struct Point(BigInteger X, BigInteger Y, BigInteger Z, BigInteger T);

    /// <summary>The public key for a 32-byte private seed.</summary>
    public static byte[] PublicKey(ReadOnlySpan<byte> seed)
    {
        var (a, _) = Expand(seed);
        return Encode(Multiply(B, a));
    }

    /// <summary>The 64-byte signature of <paramref name="message"/>, read twice (it must be seekable).</summary>
    public static byte[] Sign(ReadOnlySpan<byte> seed, Stream message)
    {
        var (a, prefix) = Expand(seed);
        var publicKey = Encode(Multiply(B, a));
        message.Position = 0;
        var r = Reduce(Hash(prefix, null, message));
        var rEncoded = Encode(Multiply(B, r));
        message.Position = 0;
        var k = Reduce(Hash(rEncoded, publicKey, message));
        var s = Mod(r + k * a, L);
        var signature = new byte[SignatureSize];
        rEncoded.CopyTo(signature, 0);
        ToBytes(s).CopyTo(signature, KeySize);
        return signature;
    }

    /// <summary>True when <paramref name="signature"/> is <paramref name="publicKey"/>'s over the rest of
    /// <paramref name="message"/>. Malformed keys and signatures are false.</summary>
    public static async Task<bool> VerifyAsync(byte[] publicKey, byte[] signature, Stream message, CancellationToken cancellation = default)
    {
        if (publicKey.Length != KeySize || signature.Length != SignatureSize) return false;
        if (Decode(publicKey, out _) is not { } a) return false;
        var rBytes = signature.AsSpan(0, KeySize).ToArray();
        if (Decode(rBytes, out _) is not { } r) return false;
        var s = FromBytes(signature.AsSpan(KeySize));
        if (s >= L) return false;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        hash.AppendData(rBytes);
        hash.AppendData(publicKey);
        var buffer = new byte[81920];
        int read;
        while ((read = await message.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0) hash.AppendData(buffer, 0, read);
        var k = Reduce(hash.GetHashAndReset());
        // [S]B = R + [k]A
        return Same(Multiply(B, s), Add(r, Multiply(a, k)));
    }

    private static (BigInteger A, byte[] Prefix) Expand(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != KeySize) throw new ArgumentException("not an Ed25519 seed (32 bytes)", nameof(seed));
        var h = SHA512.HashData(seed);
        h[0] &= 248;
        h[31] &= 127;
        h[31] |= 64;
        return (FromBytes(h.AsSpan(0, 32)), h[32..]);
    }

    private static byte[] Hash(byte[] first, byte[]? second, Stream message)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        hash.AppendData(first);
        if (second is not null) hash.AppendData(second);
        var buffer = new byte[81920];
        int read;
        while ((read = message.Read(buffer, 0, buffer.Length)) > 0) hash.AppendData(buffer, 0, read);
        return hash.GetHashAndReset();
    }

    private static BigInteger Reduce(byte[] hash) => Mod(FromBytes(hash), L);

    // MARK: Curve

    private static Point Add(Point p, Point q)
    {
        var a = Mod((p.Y - p.X) * (q.Y - q.X));
        var b = Mod((p.Y + p.X) * (q.Y + q.X));
        var c = Mod(D2 * p.T * q.T);
        var d = Mod(2 * p.Z * q.Z);
        BigInteger e = b - a, f = d - c, g = d + c, h = b + a;
        return new Point(Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h));
    }

    private static Point Multiply(Point p, BigInteger scalar)
    {
        var result = Zero;
        var addend = p;
        while (scalar > 0)
        {
            if (!scalar.IsEven) result = Add(result, addend);
            addend = Add(addend, addend);
            scalar >>= 1;
        }
        return result;
    }

    private static bool Same(Point p, Point q) =>
        Mod(p.X * q.Z - q.X * p.Z) == 0 && Mod(p.Y * q.Z - q.Y * p.Z) == 0;

    private static byte[] Encode(Point p)
    {
        var zInverse = Inverse(p.Z);
        var x = Mod(p.X * zInverse);
        var y = Mod(p.Y * zInverse);
        var bytes = ToBytes(y);
        if (!x.IsEven) bytes[31] |= 0x80;
        return bytes;
    }

    /// <summary>The point a 32-byte encoding names, or null when it names none (RFC 8032 §5.1.3).</summary>
    private static Point? Decode(ReadOnlySpan<byte> encoded, out bool sign)
    {
        var bytes = encoded.ToArray();
        sign = (bytes[31] & 0x80) != 0;
        bytes[31] &= 0x7F;
        var y = FromBytes(bytes);
        if (y >= P) return null;
        var y2 = Mod(y * y);
        var x2 = Mod((y2 - 1) * Inverse(Mod(D * y2 + 1)));
        if (x2.IsZero)
        {
            if (sign) return null;
            return new Point(0, y, 1, 0);
        }
        var x = BigInteger.ModPow(x2, (P + 3) / 8, P);
        if (Mod(x * x - x2) != 0) x = Mod(x * SqrtMinusOne);
        if (Mod(x * x - x2) != 0) return null;
        if (x.IsEven == sign) x = P - x;
        return new Point(x, y, 1, Mod(x * y));
    }

    /// <summary>The base point's encoding: y = 4/5, x even.</summary>
    private static byte[] Base() => ToBytes(Mod(4 * Inverse(5)));

    // MARK: Numbers

    private static BigInteger Mod(BigInteger value) => Mod(value, P);

    private static BigInteger Mod(BigInteger value, BigInteger modulus)
    {
        var r = BigInteger.Remainder(value, modulus);
        return r.Sign < 0 ? r + modulus : r;
    }

    private static BigInteger Inverse(BigInteger value) => BigInteger.ModPow(Mod(value), P - 2, P);

    private static BigInteger FromBytes(ReadOnlySpan<byte> littleEndian) => new(littleEndian, isUnsigned: true, isBigEndian: false);

    private static byte[] ToBytes(BigInteger value)
    {
        var bytes = new byte[32];
        value.TryWriteBytes(bytes, out _, isUnsigned: true, isBigEndian: false);
        return bytes;
    }
}
