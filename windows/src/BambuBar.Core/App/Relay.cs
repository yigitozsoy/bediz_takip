using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BambuBar.Core.App;

/// <summary>Plays PrintPulse's sound for an event (the Windows app's synthesized chimes).</summary>
public interface ISoundPlayer
{
    void Play(PrintEvent kind);
}

/// <summary>
/// PrintPulse: every print notification goes to the system notifier as before, and also plays a sound and goes to
/// Discord (a channel webhook) and Telegram (a bot's chat) when they're set up. The webhook URL and the bot token are
/// secrets, kept with the access codes (Credential Manager in the app), never in settings.json. Remote sends run on
/// a worker and never hold up the store; a failure is kept in <see cref="LastError"/> for the Notifications page.
/// </summary>
public sealed partial class RelayNotifier : INotifier, IDisposable
{
    public static class Keys
    {
        public const string Sounds = "printPulseSounds";
        public const string TelegramChat = "printPulseTelegramChat";
    }

    /// <summary>Names in the secret store (they sit beside the printers' serials, so they can't look like one).</summary>
    public const string DiscordSecret = "notify.discord";
    public const string TelegramSecret = "notify.telegram";

    private readonly INotifier _inner;
    private readonly AppSettings _settings;
    private readonly ISecretStore _secrets;
    private readonly ISoundPlayer? _sounds;
    private readonly HttpClient _http;

    public RelayNotifier(INotifier inner, AppSettings settings, ISecretStore secrets, ISoundPlayer? sounds = null, HttpMessageHandler? handler = null)
    {
        _inner = inner;
        _settings = settings;
        _secrets = secrets;
        _sounds = sounds;
        _http = new HttpClient(handler ?? new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{Brand.Name}/{AppVersion.Marketing}");
    }

    // MARK: Settings

    public string? DiscordWebhook
    {
        get => _secrets.AccessCode(DiscordSecret);
        set => Keep(DiscordSecret, value);
    }

    public string? TelegramToken
    {
        get => _secrets.AccessCode(TelegramSecret);
        set => Keep(TelegramSecret, value);
    }

    public string? TelegramChat
    {
        get => _settings.Get<string>(Keys.TelegramChat);
        set => _settings.Set(Keys.TelegramChat, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    public bool SoundsOn
    {
        get => _settings.Get(Keys.Sounds, true);
        set => _settings.Set(Keys.Sounds, value);
    }

    public bool CanPlaySounds => _sounds is not null;
    public bool DiscordReady => DiscordWebhook is { } url && IsDiscordWebhook(url);
    public bool TelegramReady => TelegramToken is { } token && IsTelegramToken(token) && TelegramChat is { } chat && IsTelegramChat(chat);

    /// <summary>Why the last remote send failed; null once one goes through.</summary>
    public string? LastError { get; private set; }

    private void Keep(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) _secrets.RemoveAccessCode(name);
        else _secrets.SetAccessCode(value.Trim(), name);
    }

    public static bool IsDiscordWebhook(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host is "discord.com" or "discordapp.com" or "ptb.discord.com" or "canary.discord.com"
        && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);

    public static bool IsTelegramToken(string token) => TokenPattern().IsMatch(token.Trim());
    public static bool IsTelegramChat(string chat) => ChatPattern().IsMatch(chat.Trim());

    [GeneratedRegex(@"^\d{5,}:[A-Za-z0-9_-]{30,}$")]
    private static partial Regex TokenPattern();
    [GeneratedRegex(@"^(-?\d{1,20}|@[A-Za-z][A-Za-z0-9_]{4,})$")]
    private static partial Regex ChatPattern();

    // MARK: INotifier

    public void RequestAuthorization() => _inner.RequestAuthorization();

    public void Post(string title, string body, string id) => Event(KindOf(id), title, body, id);

    public void Event(PrintEvent kind, string title, string body, string id)
    {
        _inner.Event(kind, title, body, id);
        PlaySound(kind);
        if (DiscordReady || TelegramReady) _ = Task.Run(() => SendAsync(kind, title, body));
    }

    public void PlaySound(PrintEvent kind, bool evenIfOff = false)
    {
        if (_sounds is null || (!evenIfOff && !SoundsOn)) return;
        try { _sounds.Play(kind); }
        catch (Exception) { /* No audio device: a missing chime isn't worth a crash. */ }
    }

    /// <summary>Sends to every channel that's set up; the first error, or null.</summary>
    public async Task<string?> SendAsync(PrintEvent kind, string title, string body)
    {
        string? error = null;
        if (DiscordReady) error ??= await SendDiscordAsync(kind, title, body).ConfigureAwait(false);
        if (TelegramReady) error ??= await SendTelegramAsync(title, body).ConfigureAwait(false);
        LastError = error;
        return error;
    }

    /// <summary>A guess for plain posts (the ids PrinterStore gives them).</summary>
    internal static PrintEvent KindOf(string id) =>
        id.StartsWith("start.", StringComparison.Ordinal) ? PrintEvent.Started
        : id.StartsWith("pause.", StringComparison.Ordinal) ? PrintEvent.Paused
        : id.StartsWith("end.", StringComparison.Ordinal) ? PrintEvent.Finished
        : id.Contains("test", StringComparison.Ordinal) ? PrintEvent.Test
        : PrintEvent.Issue;

    // MARK: Discord

    /// <summary>The embed's side colour.</summary>
    public static int ColorOf(PrintEvent kind) => kind switch
    {
        PrintEvent.Finished => 0x2ECC71,
        PrintEvent.Failed or PrintEvent.Issue => 0xE74C3C,
        PrintEvent.Canceled => 0x95A5A6,
        PrintEvent.Paused => 0xF39C12,
        _ => 0x3EA6FF,
    };

    public static JsonObject DiscordPayload(PrintEvent kind, string title, string body) => new()
    {
        ["username"] = Brand.Name,
        ["embeds"] = new JsonArray
        {
            new JsonObject
            {
                ["title"] = Clip(title, 256),
                ["description"] = Clip(body, 4000),
                ["color"] = ColorOf(kind),
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
                ["footer"] = new JsonObject { ["text"] = Brand.Name },
            },
        },
        // Nothing in a job name may ping @everyone.
        ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() },
    };

    public Task<string?> SendDiscordAsync(PrintEvent kind, string title, string body) =>
        DiscordWebhook is { } url && IsDiscordWebhook(url)
            ? PostAsync(url.Trim(), DiscordPayload(kind, title, body), discord: true)
            : Task.FromResult<string?>(Loc.T("The Discord webhook address isn't valid."));

    // MARK: Telegram

    public static JsonObject TelegramPayload(string chat, string title, string body) => new()
    {
        ["chat_id"] = chat.Trim(),
        ["text"] = Clip($"<b>{WebUtility.HtmlEncode(title)}</b>\n{WebUtility.HtmlEncode(body)}", 4000),
        ["parse_mode"] = "HTML",
        ["disable_web_page_preview"] = true,
    };

    public Task<string?> SendTelegramAsync(string title, string body)
    {
        if (TelegramToken is not { } token || !IsTelegramToken(token))
            return Task.FromResult<string?>(Loc.T("The Telegram bot token isn't valid."));
        if (TelegramChat is not { } chat || !IsTelegramChat(chat))
            return Task.FromResult<string?>(Loc.T("The Telegram chat ID isn't valid."));
        return PostAsync($"https://api.telegram.org/bot{token.Trim()}/sendMessage", TelegramPayload(chat, title, body), discord: false);
    }

    /// <summary>The chat that last wrote to the bot (getUpdates): the person sends their bot a message, then this
    /// finds the chat ID for them. Null when there's none yet.</summary>
    public async Task<(string? Chat, string? Error)> FindTelegramChatAsync(string token)
    {
        if (!IsTelegramToken(token)) return (null, Loc.T("The Telegram bot token isn't valid."));
        try
        {
            using var response = await _http.GetAsync($"https://api.telegram.org/bot{token.Trim()}/getUpdates").ConfigureAwait(false);
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (!response.IsSuccessStatusCode || json?["ok"]?.GetValue<bool>() != true)
                return (null, Describe(response.StatusCode, json?["description"]?.GetValue<string>()));
            string? chat = null;
            foreach (var update in json["result"]?.AsArray() ?? [])
            {
                var message = update?["message"] ?? update?["channel_post"] ?? update?["my_chat_member"];
                if (message?["chat"]?["id"] is { } id) chat = id.ToJsonString();
            }
            return (chat, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return (null, Offline(e));
        }
    }

    // MARK: Sending

    private async Task<string?> PostAsync(string url, JsonObject payload, bool discord)
    {
        try
        {
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) return null;
            string? reason = null;
            try
            {
                var json = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                reason = (discord ? json?["message"] : json?["description"])?.GetValue<string>();
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { }
            return Describe(response.StatusCode, reason);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return Offline(e);
        }
    }

    // Never the URL: it holds the webhook's secret or the bot's token.
    private static string Describe(HttpStatusCode status, string? reason) =>
        reason is { Length: > 0 } ? $"{(int)status}: {Clip(reason, 200)}" : $"HTTP {(int)status}";

    private static string Offline(Exception e) =>
        e is TaskCanceledException ? Loc.T("The server didn't answer in time.") : Loc.T("Couldn't reach the server.");

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    public void Dispose() => _http.Dispose();
}
