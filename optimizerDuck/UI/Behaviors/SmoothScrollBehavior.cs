using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace optimizerDuck.UI.Behaviors;

public static class SmoothScrollBehavior
{
    private sealed class ScrollState
    {
        public double TargetY;
        public double TargetX;
        public bool IsAnimating;
        public bool IsAttached;
        public long LastScrollTicks;
    }

    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> _stateTable = new();
    private static readonly List<WeakReference<ScrollViewer>> _activeScrolls = [];
    private static bool _renderingSubscribed;

    private const double SnapThreshold = 0.5;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty ScrollDeltaProperty = DependencyProperty.RegisterAttached(
        "ScrollDelta", typeof(double), typeof(SmoothScrollBehavior),
        new PropertyMetadata(48.0));

    public static readonly DependencyProperty MultiplierProperty = DependencyProperty.RegisterAttached(
        "Multiplier", typeof(double), typeof(SmoothScrollBehavior),
        new PropertyMetadata(1.0));

    public static readonly DependencyProperty SmoothingProperty = DependencyProperty.RegisterAttached(
        "Smoothing", typeof(double), typeof(SmoothScrollBehavior),
        new PropertyMetadata(0.20));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);
    public static double GetScrollDelta(DependencyObject obj) => (double)obj.GetValue(ScrollDeltaProperty);
    public static void SetScrollDelta(DependencyObject obj, double value) => obj.SetValue(ScrollDeltaProperty, value);
    public static double GetMultiplier(DependencyObject obj) => (double)obj.GetValue(MultiplierProperty);
    public static void SetMultiplier(DependencyObject obj, double value) => obj.SetValue(MultiplierProperty, value);
    public static double GetSmoothing(DependencyObject obj) => (double)obj.GetValue(SmoothingProperty);
    public static void SetSmoothing(DependencyObject obj, double value) => obj.SetValue(SmoothingProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
        {
            if ((bool)e.NewValue)
                Attach(sv);
            else
                Detach(sv);
        }
        else if (d is FrameworkElement element)
        {
            if ((bool)e.NewValue)
            {
                element.Loaded += OnElementLoaded;
                if (element.IsLoaded && FindScrollViewer(element) is { } innerSv)
                    Attach(innerSv);
            }
            else
            {
                element.Loaded -= OnElementLoaded;
                if (FindScrollViewer(element) is { } innerSv)
                    Detach(innerSv);
            }
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && FindScrollViewer(element) is { } sv)
            Attach(sv);
    }

    private static void Attach(ScrollViewer sv)
    {
        var state = _stateTable.GetOrCreateValue(sv);
        if (state.IsAttached)
            return;

        state.IsAttached = true;
        sv.PreviewMouseWheel += OnPreviewMouseWheel;
        sv.Unloaded += OnScrollViewerUnloaded;
    }

    private static void Detach(ScrollViewer sv)
    {
        if (!_stateTable.TryGetValue(sv, out var state) || !state.IsAttached)
            return;

        state.IsAttached = false;
        state.IsAnimating = false;
        sv.PreviewMouseWheel -= OnPreviewMouseWheel;
        sv.Unloaded -= OnScrollViewerUnloaded;
        RemoveActive(sv);
        _stateTable.Remove(sv);
    }

    private static void OnScrollViewerUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv && _stateTable.TryGetValue(sv, out var state))
        {
            state.IsAnimating = false;
            RemoveActive(sv);
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        try
        {
            if (sender is not ScrollViewer sv || !_stateTable.TryGetValue(sv, out var state))
                return;

            if (!sv.IsLoaded || sv.IsSealed)
                return;

            if (IsNestedScrollViewer(e.OriginalSource as DependencyObject, sv))
                return;

            e.Handled = true;

            var delta = e.Delta / 120.0;
            var scrollDelta = GetScrollDelta(sv);

            if (!state.IsAnimating)
            {
                state.TargetX = sv.HorizontalOffset;
                state.TargetY = sv.VerticalOffset;
                state.IsAnimating = true;
                _activeScrolls.Add(new WeakReference<ScrollViewer>(sv));

                if (!_renderingSubscribed)
                {
                    CompositionTarget.Rendering += OnRendering;
                    _renderingSubscribed = true;
                }
            }

            var multiplier = GetMultiplier(sv);
            var now = DateTime.UtcNow.Ticks;
            var timeSinceLastScroll = TimeSpan.FromTicks(now - state.LastScrollTicks).TotalMilliseconds;
            state.LastScrollTicks = now;

            var effectiveDelta = multiplier > 1.0 && timeSinceLastScroll < 400
                ? delta * scrollDelta * multiplier
                : delta * scrollDelta;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (sv.ScrollableWidth > 0)
                    state.TargetX = Math.Clamp(state.TargetX - effectiveDelta, 0, sv.ScrollableWidth);
            }
            else
            {
                if (sv.ScrollableHeight > 0)
                    state.TargetY = Math.Clamp(state.TargetY - effectiveDelta, 0, sv.ScrollableHeight);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SmoothScroll] PreviewMouseWheel error: {ex.Message}");
        }
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        for (var i = _activeScrolls.Count - 1; i >= 0; i--)
        {
            ScrollViewer? sv = null;

            try
            {
                var wr = _activeScrolls[i];
                if (!wr.TryGetTarget(out sv))
                {
                    _activeScrolls.RemoveAt(i);
                    continue;
                }

                if (!_stateTable.TryGetValue(sv, out var state) || !state.IsAnimating)
                {
                    _activeScrolls.RemoveAt(i);
                    continue;
                }

                if (!sv.IsLoaded)
                {
                    state.IsAnimating = false;
                    _activeScrolls.RemoveAt(i);
                    continue;
                }

                var active = false;

                var dy = state.TargetY - sv.VerticalOffset;
                if (Math.Abs(dy) > SnapThreshold)
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset + dy * GetSmoothing(sv));
                    active = true;
                }
                else if (dy != 0)
                {
                    sv.ScrollToVerticalOffset(state.TargetY);
                }

                var dx = state.TargetX - sv.HorizontalOffset;
                if (Math.Abs(dx) > SnapThreshold)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset + dx * GetSmoothing(sv));
                    active = true;
                }
                else if (dx != 0)
                {
                    sv.ScrollToHorizontalOffset(state.TargetX);
                }

                if (!active)
                {
                    state.IsAnimating = false;
                    _activeScrolls.RemoveAt(i);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmoothScroll] OnRendering error at index {i}: {ex.Message}");
                if (sv is not null && _stateTable.TryGetValue(sv, out var s))
                    s.IsAnimating = false;
                _activeScrolls.RemoveAt(i);
            }
        }

        if (_activeScrolls.Count == 0 && _renderingSubscribed)
        {
            _renderingSubscribed = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private static void RemoveActive(ScrollViewer sv)
    {
        for (var i = _activeScrolls.Count - 1; i >= 0; i--)
        {
            if (_activeScrolls[i].TryGetTarget(out var target) && target == sv)
            {
                _activeScrolls.RemoveAt(i);
                return;
            }
        }
    }

    private static bool IsNestedScrollViewer(DependencyObject? element, ScrollViewer parent)
    {
        try
        {
            while (element is not null && element != parent)
            {
                if (element is ScrollViewer sv && sv != parent && (sv.ScrollableHeight > 0 || sv.ScrollableWidth > 0))
                    return true;
                element = VisualTreeHelper.GetParent(element);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SmoothScroll] IsNestedScrollViewer error: {ex.Message}");
        }
        return false;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        try
        {
            if (element is ScrollViewer sv)
                return sv;
            var count = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < count; i++)
            {
                var result = FindScrollViewer(VisualTreeHelper.GetChild(element, i));
                if (result is not null)
                    return result;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SmoothScroll] FindScrollViewer error: {ex.Message}");
        }
        return null;
    }
}
