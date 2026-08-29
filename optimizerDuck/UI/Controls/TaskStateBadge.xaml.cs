using System.Windows;
using System.Windows.Controls;

namespace optimizerDuck.UI.Controls;

public partial class TaskStateBadge : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(string),
        typeof(TaskStateBadge),
        new PropertyMetadata(null, OnStateOrEnabledChanged)
    );

    public static readonly DependencyProperty IsReadyProperty = DependencyProperty.Register(
        nameof(IsReady),
        typeof(bool),
        typeof(TaskStateBadge),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
        nameof(IsRunning),
        typeof(bool),
        typeof(TaskStateBadge),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty IsDisabledStateProperty = DependencyProperty.Register(
        nameof(IsDisabledState),
        typeof(bool),
        typeof(TaskStateBadge),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty IsEnabledStateProperty = DependencyProperty.Register(
        nameof(IsEnabledState),
        typeof(bool?),
        typeof(TaskStateBadge),
        new PropertyMetadata(null, OnStateOrEnabledChanged)
    );

    public static readonly DependencyProperty EffectiveStateDisplayProperty =
        DependencyProperty.Register(
            nameof(EffectiveStateDisplay),
            typeof(string),
            typeof(TaskStateBadge),
            new PropertyMetadata(null)
        );

    public TaskStateBadge()
    {
        InitializeComponent();
    }

    public string? State
    {
        get => (string?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public bool IsReady
    {
        get => (bool)GetValue(IsReadyProperty);
        set => SetValue(IsReadyProperty, value);
    }

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public bool IsDisabledState
    {
        get => (bool)GetValue(IsDisabledStateProperty);
        set => SetValue(IsDisabledStateProperty, value);
    }

    public bool? IsEnabledState
    {
        get => (bool?)GetValue(IsEnabledStateProperty);
        set => SetValue(IsEnabledStateProperty, value);
    }

    public string? EffectiveStateDisplay
    {
        get => (string?)GetValue(EffectiveStateDisplayProperty);
        private set => SetValue(EffectiveStateDisplayProperty, value);
    }

    private static void OnStateOrEnabledChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is TaskStateBadge badge)
            badge.UpdateEffectiveStateDisplay();
    }

    private void UpdateEffectiveStateDisplay()
    {
        if (IsEnabledState.HasValue)
        {
            EffectiveStateDisplay = IsEnabledState.Value
                ? optimizerDuck.Resources.Languages.Translations.Common_Toggle_On
                : optimizerDuck.Resources.Languages.Translations.Common_Toggle_Off;
        }
        else
        {
            EffectiveStateDisplay = State;
        }
    }
}
