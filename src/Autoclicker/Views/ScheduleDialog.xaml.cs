using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Autoclicker.Localization;
using Autoclicker.Scheduling;

namespace Autoclicker.Views;

/// <summary>Programar los clics o una macro: cuándo empezar y qué hacer al terminar.</summary>
public partial class ScheduleDialog : Window
{
    private readonly Schedule _schedule;
    private bool _accepted;

    private ScheduleDialog(string title, Schedule last)
    {
        InitializeComponent();
        TitleText.Text = title;
        _schedule = new Schedule { Mode = last.Mode, Hour = last.Hour, Minute = last.Minute, InMinutes = last.InMinutes, End = last.End };

        (_schedule.Mode switch { StartMode.Now => NowRadio, StartMode.In => InRadio, _ => AtRadio }).IsChecked = true;
        HourBox.Text = _schedule.Hour.ToString("00", CultureInfo.InvariantCulture);
        MinuteBox.Text = _schedule.Minute.ToString("00", CultureInfo.InvariantCulture);
        InBox.Text = _schedule.InMinutes.ToString(CultureInfo.InvariantCulture);
        InUnitText.Text = Schedule.TestSeconds ? "s" : "min";
        foreach (var box in new[] { HourBox, MinuteBox, InBox })
        {
            box.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
        }
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not ButtonBase) DragMove();
        };
        Refresh();
    }

    /// <summary>Devuelve la programación elegida, o null si se canceló.</summary>
    public static Schedule? Ask(Window owner, string title, Schedule last)
    {
        var dialog = new ScheduleDialog(title, last) { Owner = owner };
        dialog.ShowDialog();
        return dialog._accepted ? dialog._schedule : null;
    }

    private void Refresh()
    {
        // Los campos avisan de cambios ya durante InitializeComponent, antes de que exista todo
        if (_schedule == null || OkButton == null || EndText == null || WhenText == null) return;
        AtPanel.Visibility = AtRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        InPanel.Visibility = InRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Content = Strings.T(NowRadio.IsChecked == true ? "ScheduleStartNow" : "ScheduleOk");
        EndText.Text = Strings.T(Schedule.EndKey(_schedule.End));
        if (TryReadTime(out int h, out int m))
        {
            DateTime now = DateTime.Now;
            WhenText.Text = Strings.T(Schedule.NextAt(now, h, m).Date > now.Date ? "ScheduleTomorrow" : "ScheduleToday");
        }
        else WhenText.Text = "";
    }

    private bool TryReadTime(out int hour, out int minute)
    {
        bool ok = int.TryParse(HourBox.Text, out hour) & int.TryParse(MinuteBox.Text, out minute);
        return ok && hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    private void Mode_Checked(object sender, RoutedEventArgs e) => Refresh();

    private void Time_Changed(object sender, TextChangedEventArgs e) => Refresh();

    private void End_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = EndButton, Placement = PlacementMode.Bottom, MinWidth = EndButton.ActualWidth };
        foreach (EndAction action in Enum.GetValues(typeof(EndAction)))
        {
            var item = new MenuItem { Header = Strings.T(Schedule.EndKey(action)), IsChecked = action == _schedule.End };
            EndAction chosen = action;
            item.Click += (_, _) => { _schedule.End = chosen; Refresh(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _schedule.Mode = NowRadio.IsChecked == true ? StartMode.Now : InRadio.IsChecked == true ? StartMode.In : StartMode.At;
        if (_schedule.Mode == StartMode.At)
        {
            if (!TryReadTime(out int h, out int m)) { HourBox.Focus(); return; }
            (_schedule.Hour, _schedule.Minute) = (h, m);
        }
        if (_schedule.Mode == StartMode.In)
        {
            if (!int.TryParse(InBox.Text, out int minutes) || minutes < 1) { InBox.Focus(); return; }
            _schedule.InMinutes = minutes;
        }
        _accepted = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
