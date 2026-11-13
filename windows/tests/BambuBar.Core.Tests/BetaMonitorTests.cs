using System.Net;
using System.Text.Json.Nodes;
using BambuBar.Core.App;
using BambuBar.Core.Model;

namespace BambuBar.Core.Tests;

/// <summary>beta's additions: the Discord/Telegram relay, the chimes and the history totals.</summary>
public class BetaMonitorTests
{
    private const string Webhook = "https://discord.com/api/webhooks/123456789/abcdefghijklmnop";
    private const string Token = "123456789:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsawABC";

    private sealed class Recorder : INotifier
    {
        public List<(PrintEvent Kind, string Title)> Events { get; } = [];
        public void Post(string title, string body, string id) => Events.Add((PrintEvent.Test, title));
        public void Event(PrintEvent kind, string title, string body, string id) => Events.Add((kind, title));
        public void RequestAuthorization() { }
    }

    private sealed class Sounds : ISoundPlayer
    {
        public List<PrintEvent> Played { get; } = [];
        public void Play(PrintEvent kind) => Played.Add(kind);
    }

    private sealed class Server(HttpStatusCode status = HttpStatusCode.OK, string reply = "{\"ok\":true}") : HttpMessageHandler
    {
        public System.Collections.Concurrent.ConcurrentQueue<(Uri Url, string Body)> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Enqueue((request.RequestUri!, request.Content is null ? "" : await request.Content.ReadAsStringAsync(token)));
            return new HttpResponseMessage(status) { Content = new StringContent(reply) };
        }
    }

    [Theory]
    [InlineData(Webhook, true)]
    [InlineData("https://discordapp.com/api/webhooks/1/x", true)]
    [InlineData("http://discord.com/api/webhooks/1/x", false)]
    [InlineData("https://discord.com.evil.example/api/webhooks/1/x", false)]
    [InlineData("https://discord.com/channels/1/2", false)]
    [InlineData("not a url", false)]
    public void DiscordWebhooksAreChecked(string url, bool valid) => Assert.Equal(valid, RelayNotifier.IsDiscordWebhook(url));

    [Theory]
    [InlineData("123456789", true)]
    [InlineData("-1001234567890", true)]
    [InlineData("@my_channel", true)]
    [InlineData("12ab", false)]
    [InlineData("", false)]
    public void TelegramChatsAreChecked(string chat, bool valid) => Assert.Equal(valid, RelayNotifier.IsTelegramChat(chat));

    [Fact]
    public void TelegramTokensAreChecked()
    {
        Assert.True(RelayNotifier.IsTelegramToken(Token));
        Assert.False(RelayNotifier.IsTelegramToken("123:short"));
    }

    [Fact]
    public async Task EventsReachEveryChannel()
    {
        var inner = new Recorder();
        var sounds = new Sounds();
        var server = new Server();
        var secrets = new MemorySecretStore();
        using var relay = new RelayNotifier(inner, new AppSettings(null), secrets, sounds, server)
        {
            DiscordWebhook = Webhook, TelegramToken = Token, TelegramChat = "42",
        };
        Assert.Equal(Webhook, secrets.AccessCode(RelayNotifier.DiscordSecret));
        Assert.True(relay.DiscordReady && relay.TelegramReady);

        relay.Event(PrintEvent.Finished, "Print finished", "Benchy <done> & ready", "end.X");
        Assert.Equal([(PrintEvent.Finished, "Print finished")], inner.Events);
        Assert.Equal([PrintEvent.Finished], sounds.Played);

        Assert.Null(await relay.SendAsync(PrintEvent.Finished, "Print finished", "Benchy <done> & ready"));
        var discord = server.Requests.Last(r => r.Url.Host == "discord.com");
        var embed = JsonNode.Parse(discord.Body)!["embeds"]![0]!;
        Assert.Equal("Print finished", embed["title"]!.GetValue<string>());
        Assert.Equal(RelayNotifier.ColorOf(PrintEvent.Finished), embed["color"]!.GetValue<int>());
        var telegram = server.Requests.Last(r => r.Url.Host == "api.telegram.org");
        Assert.EndsWith("/sendMessage", telegram.Url.AbsolutePath);
        var text = JsonNode.Parse(telegram.Body)!["text"]!.GetValue<string>();
        Assert.Equal("<b>Print finished</b>\nBenchy &lt;done&gt; &amp; ready", text);
    }

    [Fact]
    public void SoundsCanBeTurnedOff()
    {
        var sounds = new Sounds();
        using var relay = new RelayNotifier(new Recorder(), new AppSettings(null), new MemorySecretStore(), sounds, new Server()) { SoundsOn = false };
        relay.Event(PrintEvent.Failed, "t", "b", "end.X");
        Assert.Empty(sounds.Played);
        relay.PlaySound(PrintEvent.Failed, evenIfOff: true);
        Assert.Equal([PrintEvent.Failed], sounds.Played);
    }

    [Fact]
    public async Task FailuresSayWhyWithoutTheSecret()
    {
        var server = new Server(HttpStatusCode.Unauthorized, "{\"ok\":false,\"description\":\"Unauthorized\"}");
        using var relay = new RelayNotifier(new Recorder(), new AppSettings(null), new MemorySecretStore(), null, server)
        {
            TelegramToken = Token, TelegramChat = "42",
        };
        var error = await relay.SendTelegramAsync("t", "b");
        Assert.Equal("401: Unauthorized", error);
        Assert.DoesNotContain(Token, error);
        Assert.Equal(error, await relay.SendAsync(PrintEvent.Test, "t", "b"));
        Assert.Equal(error, relay.LastError);
    }

    [Fact]
    public async Task TheBotsLastChatIsFound()
    {
        var server = new Server(reply: "{\"ok\":true,\"result\":[{\"message\":{\"chat\":{\"id\":111}}},{\"message\":{\"chat\":{\"id\":-222}}}]}");
        using var relay = new RelayNotifier(new Recorder(), new AppSettings(null), new MemorySecretStore(), null, server);
        var (chat, error) = await relay.FindTelegramChatAsync(Token);
        Assert.Null(error);
        Assert.Equal("-222", chat);
    }

    [Fact]
    public void ClearingASecretRemovesIt()
    {
        var secrets = new MemorySecretStore();
        using var relay = new RelayNotifier(new Recorder(), new AppSettings(null), secrets) { DiscordWebhook = Webhook };
        relay.DiscordWebhook = " ";
        Assert.Null(secrets.AccessCode(RelayNotifier.DiscordSecret));
        Assert.False(relay.DiscordReady);
    }

    [Theory]
    [InlineData(PrintEvent.Finished)]
    [InlineData(PrintEvent.Failed)]
    [InlineData(PrintEvent.Paused)]
    [InlineData(PrintEvent.Issue)]
    public void ChimesAreValidWaves(PrintEvent kind)
    {
        var wave = Chime.Wave(kind);
        Assert.Equal("RIFF"u8.ToArray(), wave[..4]);
        Assert.Equal("WAVE"u8.ToArray(), wave[8..12]);
        Assert.Equal(wave.Length - 8, BitConverter.ToInt32(wave, 4));
        var seconds = (wave.Length - 44) / 2.0 / Chime.SampleRate;
        Assert.InRange(seconds, 0.4, 2);
        // Loud enough to hear, never clipped.
        var peak = Enumerable.Range(0, (wave.Length - 44) / 2).Max(i => Math.Abs((int)BitConverter.ToInt16(wave, 44 + i * 2)));
        Assert.InRange(peak, 8000, short.MaxValue);
    }

    [Fact]
    public void HistoryTotals()
    {
        var now = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        JobRecord Job(string name, JobOutcome outcome, double daysAgo, int minutes, string? material) => new()
        {
            Name = name, Outcome = outcome, StartedAt = now.AddDays(-daysAgo).AddMinutes(-minutes), EndedAt = outcome == JobOutcome.Running ? null : now.AddDays(-daysAgo),
            FilamentMaterial = material,
        };
        var stats = HistoryStats.Of(
        [
            Job("A", JobOutcome.Finished, 1, 60, "PLA"),
            Job("B", JobOutcome.Finished, 2, 300, "pla"),
            Job("C", JobOutcome.Failed, 3, 30, "PETG"),
            Job("D", JobOutcome.Canceled, 10, 10, null),
            Job("E", JobOutcome.Ended, 20, 0, "PLA"),
            Job("F", JobOutcome.Running, 0, 5, "ABS"),
        ], now);
        Assert.Equal(5, stats.Total);
        Assert.Equal((2, 1, 1, 1), (stats.Finished, stats.Failed, stats.Canceled, stats.Unknown));
        Assert.Equal(0.5, stats.SuccessRate);
        Assert.Equal(TimeSpan.FromMinutes(400), stats.PrintTime);
        Assert.Equal("B", stats.Longest?.Name);
        Assert.Equal(3, stats.LastWeek);
        Assert.Equal([("PLA", 3), ("PETG", 1)], stats.Materials);
        Assert.Null(HistoryStats.Of([], now).SuccessRate);
    }

    [Fact]
    public void BetaMonitorKeepsMoreHistory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pp-history-{Guid.NewGuid():N}.json");
        try
        {
            var store = new HistoryStore(path, HistoryStore.BetaMonitorLimit);
            store.Save(Enumerable.Range(0, 40).Select(i => new JobRecord { Name = $"J{i}", StartedAt = DateTimeOffset.Now, Outcome = JobOutcome.Finished, PrinterSerial = "A" }));
            Assert.Equal(40, store.Load().Count);
        }
        finally { File.Delete(path); }
    }
}
