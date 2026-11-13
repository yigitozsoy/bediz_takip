using System.Windows;
using System.Windows.Controls;
using BambuBar.Core;
using BambuBar.Core.App;
using BambuBar.Theme;
using Microsoft.Win32;

namespace BambuBar.AppWindow;

/// <summary>Which print events post a notification, and a way to Windows Settings when Windows has them turned off
/// (the Mac's NotificationsPage).</summary>
internal sealed class NotificationsPage(WindowContext ctx) : Page(ctx)
{
    private readonly Region _banner = new();
    private DateTime _checkedAt = DateTime.MinValue;

    public override FrameworkElement Build()
    {
        var group = W.Group(Loc.T("Notify me when"), "", out var body);
        body.Children.Add(W.Switch(Loc.T("Print finished"), Store.NotifyFinished, on => Store.NotifyFinished = on));
        body.Children.Add(W.Switch(Loc.T("Print failed, canceled or an error"), Store.NotifyFailed, on => Store.NotifyFailed = on));
        body.Children.Add(W.Switch(Loc.T("Print paused (filament runout, user)"), Store.NotifyPaused, on => Store.NotifyPaused = on));
        body.Children.Add(W.Switch(Loc.T("Print started"), Store.NotifyStarted, on => Store.NotifyStarted = on));
        // Windows only: a sample, to see where notifications appear and how they sound.
        if (Ctx.Services.TestNotification is { } test)
        {
            body.Children.Add(W.Divider());
            body.Children.Add(W.Row(Loc.T("Send a sample notification to see how it looks."), W.Button(Loc.T("Send Test Notification"), test, small: true)));
        }
        var page = new Stack
        {
            Spacing = 22,
            Children =
            {
                W.PageHeader(Loc.T("Notifications"), Loc.T("Bambu Bar tells you when a print needs you, even while its window is closed.")),
                _banner.Host, group,
            },
        };
        // PrintPulse: sounds, Discord and Telegram, for the same events as the switches above.
        if (Ctx.Services.Relay is { } relay)
        {
            if (relay.CanPlaySounds) page.Children.Add(Sounds(relay));
            page.Children.Add(Discord(relay));
            page.Children.Add(Telegram(relay));
        }
        return page;
    }

    private static FrameworkElement Sounds(RelayNotifier relay)
    {
        var group = W.Group(Loc.T("Sounds"), "\uE767", out var body);
        body.Children.Add(W.Switch(Loc.T("Play a sound with each notification"), relay.SoundsOn, on => relay.SoundsOn = on));
        var previews = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (kind, name) in new[] { (PrintEvent.Finished, Loc.T("Finished", "How a print ended")), (PrintEvent.Failed, Loc.T("Failed", "How a print ended")), (PrintEvent.Paused, Loc.T("Paused")), (PrintEvent.Started, Loc.T("Print started")) })
        {
            var play = W.Button(name, () => relay.PlaySound(kind, evenIfOff: true), small: true, glyph: "\uE768");
            play.Margin = new Thickness(6, 0, 0, 0);
            previews.Children.Add(play);
        }
        body.Children.Add(W.Row(Loc.T("Listen"), previews));
        return group;
    }

    private static FrameworkElement Discord(RelayNotifier relay)
    {
        var group = W.Group("Discord", "\uE8BD", out var body);
        var url = new TextField(Loc.T("Webhook URL"), secret: true, width: 280) { Text = relay.DiscordWebhook ?? "" };
        var status = W.Note("");
        Say(status, relay.DiscordReady ? Loc.T("Connected. Notifications also go to this channel.") : "");
        bool Save()
        {
            var text = url.Text.Trim();
            if (text.Length > 0 && !RelayNotifier.IsDiscordWebhook(text))
            {
                Say(status, Loc.T("The Discord webhook address isn't valid."));
                return false;
            }
            relay.DiscordWebhook = text;
            Say(status, text.Length == 0 ? Loc.T("Removed.") : Loc.T("Saved."));
            return text.Length > 0;
        }
        async void Test()
        {
            if (!Save()) return;
            Say(status, Loc.T("Sending…"));
            var error = await relay.SendDiscordAsync(PrintEvent.Test, Loc.T("Notifications are on"), Loc.T("This is how PrintPulse tells you a print finished, failed or needs you."));
            Say(status, error is null ? Loc.T("Sent. Check your Discord channel.") : Loc.F("Couldn't send: %@", error));
        }
        body.Children.Add(W.Row(Loc.T("Webhook URL"), url));
        body.Children.Add(W.Row("", Buttons(W.Button(Loc.T("Save"), () => Save(), small: true), W.Button(Loc.T("Send Test"), Test, small: true))));
        body.Children.Add(status);
        body.Children.Add(W.Note(Loc.T("In Discord: Server Settings → Integrations → Webhooks → New Webhook, then Copy Webhook URL. It's kept in Windows Credential Manager.")));
        return group;
    }

    private static FrameworkElement Telegram(RelayNotifier relay)
    {
        var group = W.Group("Telegram", "\uE724", out var body);
        var token = new TextField(Loc.T("Bot token"), secret: true, width: 280) { Text = relay.TelegramToken ?? "" };
        var chat = new TextField(Loc.T("Chat ID"), width: 180) { Text = relay.TelegramChat ?? "" };
        var status = W.Note("");
        Say(status, relay.TelegramReady ? Loc.T("Connected. Notifications also go to this chat.") : "");
        bool Save()
        {
            var t = token.Text.Trim();
            var c = chat.Text.Trim();
            if (t.Length > 0 && !RelayNotifier.IsTelegramToken(t)) { Say(status, Loc.T("The Telegram bot token isn't valid.")); return false; }
            if (c.Length > 0 && !RelayNotifier.IsTelegramChat(c)) { Say(status, Loc.T("The Telegram chat ID isn't valid.")); return false; }
            relay.TelegramToken = t;
            relay.TelegramChat = c;
            Say(status, t.Length == 0 && c.Length == 0 ? Loc.T("Removed.") : Loc.T("Saved."));
            return t.Length > 0 && c.Length > 0;
        }
        async void Find()
        {
            Say(status, Loc.T("Looking…"));
            var (found, error) = await relay.FindTelegramChatAsync(token.Text.Trim());
            if (found is not null) chat.Text = found;
            Say(status, error is not null ? Loc.F("Couldn't send: %@", error)
                : found is null ? Loc.T("No messages yet. Send your bot a message in Telegram, then try again.")
                : Loc.T("Found your chat. Press Save or Send Test."));
        }
        async void Test()
        {
            if (!Save()) { if (status.Text == Loc.T("Saved.")) Say(status, Loc.T("Enter both the bot token and the chat ID.")); return; }
            Say(status, Loc.T("Sending…"));
            var error = await relay.SendTelegramAsync(Loc.T("Notifications are on"), Loc.T("This is how PrintPulse tells you a print finished, failed or needs you."));
            Say(status, error is null ? Loc.T("Sent. Check Telegram.") : Loc.F("Couldn't send: %@", error));
        }
        body.Children.Add(W.Row(Loc.T("Bot token"), token));
        var find = W.Button(Loc.T("Find"), Find, small: true);
        find.Margin = new Thickness(8, 0, 0, 0);
        body.Children.Add(W.Row(Loc.T("Chat ID"), new StackPanel { Orientation = Orientation.Horizontal, Children = { chat, find } }));
        body.Children.Add(W.Row("", Buttons(W.Button(Loc.T("Save"), () => Save(), small: true), W.Button(Loc.T("Send Test"), Test, small: true))));
        body.Children.Add(status);
        body.Children.Add(W.Note(Loc.T("Make a bot with @BotFather and paste its token. Send your bot any message, then press Find to fill in the chat ID.")));
        return group;
    }

    /// <summary>A status line that takes no room while it has nothing to say.</summary>
    private static void Say(TextBlock status, string text)
    {
        status.Text = text;
        status.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static StackPanel Buttons(params FrameworkElement[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons)
        {
            b.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(b);
        }
        return row;
    }

    /// <summary>Checks Windows' switches again now and then, so the banner goes once they're back on.</summary>
    public override void Refresh()
    {
        if (DateTime.UtcNow - _checkedAt < TimeSpan.FromSeconds(2)) return;
        _checkedAt = DateTime.UtcNow;
        var off = !Ctx.Services.Offscreen && TurnedOff(Ctx.Services.ToastsEnabled);
        _banner.Show(off.ToString(), () => off
            ? W.InfoBar(Palette.Paused, "", Loc.T("Notifications for Bambu Bar are turned off in Windows Settings."), Ctx,
                W.Button(Loc.T("Open Settings"), () => W.Open("ms-settings:notifications"), small: true))
            : null);
    }

    /// <summary>Notifications are off for every app (Settings → System → Notifications), or for Bambu Bar's entry
    /// there. Asks Windows for Bambu Bar's AppUserModelID (<paramref name="toasts"/>, the app's toast notifier); where
    /// that can't answer, reads what the Settings page writes in the registry.</summary>
    internal static bool TurnedOff(Func<bool?>? toasts)
    {
        if (toasts?.Invoke() is { } allowed) return !allowed;
        try
        {
            using (var push = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications"))
            {
                if (push?.GetValue("ToastEnabled") is int enabled && enabled == 0) return true;
            }
            using var app = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\{Notifications.AppIdentity.Id}");
            return app?.GetValue("Enabled") is int on && on == 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }
}
