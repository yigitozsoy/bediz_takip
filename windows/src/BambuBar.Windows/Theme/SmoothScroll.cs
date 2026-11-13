using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BambuBar.Theme;

/// <summary>
/// beta: smooth wheel scrolling for every <see cref="ScrollViewer"/> in the app. WPF's own moves a fixed three lines
/// (48 px) per wheel message whatever its size, so a precision touchpad, which sends many small ones, jumps and
/// races. Here the distance follows the message's delta: a touchpad's small steps move at once, a mouse wheel's
/// notches glide to their place over a few frames (at once when Windows' animations are off).
/// </summary>
public static class SmoothScroll
{
    private sealed class Glide
    {
        public double Target;
        public double Last;
        public TimeSpan Time;
    }

    private static readonly ConditionalWeakTable<ScrollViewer, Glide> Glides = new();
    private static readonly List<ScrollViewer> Moving = [];
    private static bool _installed, _hooked;

    /// <summary>Time constant of a notch's glide: about 95 % of the way after three of these.</summary>
    private const double Tau = 0.055;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.MouseWheelEvent, new MouseWheelEventHandler(OnWheel));
    }

    /// <summary>Pixels per wheel delta of 120 (one notch): Windows' "lines to scroll" at WPF's 16 px a line, or a
    /// page when it is set to scroll a screen at a time.</summary>
    private static double PerNotch(ScrollViewer scroller)
    {
        var lines = SystemParameters.WheelScrollLines;
        return lines > 0 ? lines * 16.0 : Math.Max(48, scroller.ViewportHeight * 0.9);
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        // Logical (item) scrolling and zoom gestures keep WPF's handling.
        if (e.Handled || sender is not ScrollViewer scroller || scroller.CanContentScroll) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        if (scroller.ScrollableHeight <= 0) return;
        var glide = Glides.GetOrCreateValue(scroller);
        var moving = Moving.Contains(scroller);
        var from = moving ? glide.Target : scroller.VerticalOffset;
        var target = Math.Clamp(from - e.Delta / 120.0 * PerNotch(scroller), 0, scroller.ScrollableHeight);
        // Handled at an end too, as WPF's own does: an outer scroller doesn't take over mid-gesture.
        e.Handled = true;
        if (Math.Abs(target - scroller.VerticalOffset) < 0.5 && !moving) return;

        // A touchpad's steps (not whole notches) arrive many a second: follow them as they come.
        if (e.Delta % 120 != 0 || !AppTheme.AnimationsEnabled)
        {
            if (moving) Moving.Remove(scroller);
            scroller.ScrollToVerticalOffset(target);
            return;
        }
        glide.Target = target;
        if (!moving)
        {
            glide.Last = scroller.VerticalOffset;
            glide.Time = TimeSpan.Zero;
            Moving.Add(scroller);
        }
        if (!_hooked)
        {
            _hooked = true;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    private static void OnFrame(object? sender, EventArgs e)
    {
        var now = e is RenderingEventArgs r ? r.RenderingTime : TimeSpan.Zero;
        for (var i = Moving.Count - 1; i >= 0; i--)
        {
            var scroller = Moving[i];
            if (!Glides.TryGetValue(scroller, out var glide) || !scroller.IsVisible
                // Someone else moved it (the scroll bar, the keyboard, a page change): their move wins.
                || Math.Abs(scroller.VerticalOffset - glide.Last) > 1)
            {
                Moving.RemoveAt(i);
                continue;
            }
            // The first frame only notes the time; WPF may call twice in one frame, with the same time.
            if (glide.Time == TimeSpan.Zero || now <= glide.Time)
            {
                if (glide.Time == TimeSpan.Zero) glide.Time = now;
                continue;
            }
            var dt = Math.Min((now - glide.Time).TotalSeconds, 0.1);
            glide.Time = now;
            var target = Math.Clamp(glide.Target, 0, scroller.ScrollableHeight);
            var next = glide.Last + (target - glide.Last) * (1 - Math.Exp(-dt / Tau));
            if (Math.Abs(target - next) < 0.5)
            {
                next = target;
                Moving.RemoveAt(i);
            }
            glide.Last = next;
            scroller.ScrollToVerticalOffset(next);
        }
        if (Moving.Count == 0)
        {
            CompositionTarget.Rendering -= OnFrame;
            _hooked = false;
        }
    }
}
