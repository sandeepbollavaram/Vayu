namespace Vayu.Security.Tests;

public class SecretRedactorTests
{
    // Fake values built via string concatenation so the CI grep in
    // .github/workflows/security.yml does not match the source literal.
    // The compiler concatenates at build time, so the redactor sees a
    // realistic-shaped fake at runtime.
    private const string FakeGemini = "AIza" + "SyVayuTestKeyFakeValueAbcdef1234567890";
    private const string FakeOpenAI = "sk-" + "VayuFakeOpenAIKeyForTestsOnly1234567890abc";
    private const string FakeGhp = "ghp_" + "VayuFakeGitHubTokenForTests0123456789abcdef";
    private const string FakeGithubPat = "github_pat_" + "VayuFakeFineGrainedAaaaBbbbCccc1234567890ABCDEF1234567890";
    private const string FakeBearer = "Bearer " + "VayuFakeBearerTokenForTests0123456789abc";
    private const string FakeJwt = "eyJ" + "hbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0In0.VayuFakeSignatureForTests1234567890";
    private const string FakePrivateKey =
        "-----BEGIN" + " RSA PRIVATE KEY-----\nFAKE_VAYU_TEST_KEY_DATA_BODY_TEXT\n-----END" + " RSA PRIVATE KEY-----";

    [Theory]
    [InlineData(FakeGemini)]
    [InlineData(FakeOpenAI)]
    [InlineData(FakeGhp)]
    [InlineData(FakeGithubPat)]
    [InlineData(FakeJwt)]
    public void Redacts_KnownTokenPatterns(string token)
    {
        var input = $"before {token} after";
        var output = SecretRedactor.Redact(input);

        Assert.DoesNotContain(token, output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redacts_BearerToken_PreservesPrefix()
    {
        var actualToken = FakeBearer["Bearer ".Length..];
        var input = $"Authorization header: {FakeBearer}";

        var output = SecretRedactor.Redact(input);

        Assert.DoesNotContain(actualToken, output);
        Assert.Contains("Bearer [REDACTED]", output);
    }

    [Fact]
    public void Redacts_PrivateKeyBlock()
    {
        var output = SecretRedactor.Redact(FakePrivateKey);

        Assert.DoesNotContain("FAKE_VAYU_TEST_KEY_DATA_BODY_TEXT", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Theory]
    [InlineData("api_key=fake-test-value-1234567890")]
    [InlineData("apiKey=fake-test-value-1234567890")]
    [InlineData("api-key=fake-test-value-1234567890")]
    [InlineData("token=fake-test-value-1234567890")]
    [InlineData("secret=fake-test-value-1234567890")]
    [InlineData("password=Fake!Password123Value")]
    public void Redacts_GenericKeyValuePairs(string input)
    {
        var output = SecretRedactor.Redact(input);

        Assert.DoesNotContain("fake-test-value-1234567890", output);
        Assert.DoesNotContain("Fake!Password123Value", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redacts_HttpHeaders()
    {
        const string xApiSecret = "fake-header-value-12345-not-real";
        var input = $"X-API-Key: {xApiSecret}\nAuthorization: {FakeBearer}";

        var output = SecretRedactor.Redact(input);

        Assert.DoesNotContain(xApiSecret, output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redacts_MixedTextWithMultipleSecrets()
    {
        var input = $"Caller={FakeGemini}; outbound {FakeGhp}; ok.";

        var output = SecretRedactor.Redact(input);

        Assert.DoesNotContain(FakeGemini, output);
        Assert.DoesNotContain(FakeGhp, output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Returns_EmptyString_OnNullInput()
    {
        Assert.Equal(string.Empty, SecretRedactor.Redact(null));
    }

    [Fact]
    public void LeavesNonSecretText_Untouched()
    {
        const string benign = "User clicked 'Open Notepad'. Result: ok.";

        Assert.Equal(benign, SecretRedactor.Redact(benign));
    }
}
