using BambuBar.Core.Updates;

namespace BambuBar.Core.Tests.Updates;

/// <summary>beta's own Ed25519 against RFC 8032 §7.1's test vectors, and its rejections.</summary>
public class Ed25519Tests
{
    public static TheoryData<string, string, string, string> Vectors => new()
    {
        // TEST 1: the empty message.
        {
            "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60",
            "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a",
            "",
            "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"
        },
        // TEST 2: one byte.
        {
            "4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb",
            "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c",
            "72",
            "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00"
        },
        // TEST 3: two bytes.
        {
            "c5aa8df43f9f837bedb7442f31dcb7b166d38535076f094b85ce3a2e0b4458f7",
            "fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025",
            "af82",
            "6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a"
        },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public async Task MatchesRfc8032(string seed, string publicKey, string message, string signature)
    {
        var key = Convert.FromHexString(seed);
        var body = Convert.FromHexString(message);
        Assert.Equal(publicKey, Convert.ToHexStringLower(Ed25519.PublicKey(key)));
        Assert.Equal(signature, Convert.ToHexStringLower(Ed25519.Sign(key, new MemoryStream(body))));
        Assert.True(await Ed25519.VerifyAsync(Convert.FromHexString(publicKey), Convert.FromHexString(signature), new MemoryStream(body),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsChangedMessagesAndSignatures()
    {
        var ct = TestContext.Current.CancellationToken;
        var seed = Convert.FromHexString("4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb");
        var key = Ed25519.PublicKey(seed);
        var signature = Ed25519.Sign(seed, new MemoryStream([0x72]));
        Assert.False(await Ed25519.VerifyAsync(key, signature, new MemoryStream([0x73]), ct));
        var flipped = (byte[])signature.Clone();
        flipped[40] ^= 1;
        Assert.False(await Ed25519.VerifyAsync(key, flipped, new MemoryStream([0x72]), ct));
        // S ≥ L (a malleated signature) and a key that isn't a point.
        var big = (byte[])signature.Clone();
        for (var i = 32; i < 64; i++) big[i] = 0xFF;
        Assert.False(await Ed25519.VerifyAsync(key, big, new MemoryStream([0x72]), ct));
        var notAPoint = new byte[32];
        notAPoint[0] = 2;
        Assert.False(await Ed25519.VerifyAsync(notAPoint, signature, new MemoryStream([0x72]), ct));
    }
}
