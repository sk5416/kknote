using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KkNote.Converters;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class TodoRow : UserControl
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(TodoItem), typeof(TodoRow),
        new PropertyMetadata(null, (d, e) => ((TodoRow)d).OnItemChanged((TodoItem?)e.OldValue, (TodoItem?)e.NewValue)));

    public static readonly DependencyProperty StoreProperty = DependencyProperty.Register(
        nameof(Store), typeof(NoteStore), typeof(TodoRow),
        new PropertyMetadata(null, (d, _) => ((TodoRow)d).Apply()));

    public static readonly DependencyProperty HostPageProperty = DependencyProperty.Register(
        nameof(HostPage), typeof(Note), typeof(TodoRow),
        new PropertyMetadata(null, (d, _) => ((TodoRow)d).Apply()));

    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
        nameof(Index), typeof(int), typeof(TodoRow),
        new PropertyMetadata(0, (d, _) => ((TodoRow)d).Apply()));

    public static readonly DependencyProperty DarkProperty = DependencyProperty.Register(
        nameof(Dark), typeof(bool), typeof(TodoRow),
        new PropertyMetadata(false, (d, _) => ((TodoRow)d).Apply()));

    public static readonly DependencyProperty PoolDeleteProperty = DependencyProperty.Register(
        nameof(PoolDelete), typeof(bool), typeof(TodoRow),
        new PropertyMetadata(false, (d, _) => ((TodoRow)d).Apply()));

    public const string DragFormat = "KkNote.TodoId";

    public event Action<string, string>? TodoDropped;

    private bool _internal;
    private bool _picking;
    private bool _dragArmed;
    private Point _dragStart;
    private Brush? _restBorder;

    public TodoRow()
    {
        InitializeComponent();
    }

    public TodoItem? Item
    {
        get => (TodoItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public NoteStore? Store
    {
        get => (NoteStore?)GetValue(StoreProperty);
        set => SetValue(StoreProperty, value);
    }

    public Note? HostPage
    {
        get => (Note?)GetValue(HostPageProperty);
        set => SetValue(HostPageProperty, value);
    }

    public int Index
    {
        get => (int)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    public bool Dark
    {
        get => (bool)GetValue(DarkProperty);
        set => SetValue(DarkProperty, value);
    }

    public bool PoolDelete
    {
        get => (bool)GetValue(PoolDeleteProperty);
        set => SetValue(PoolDeleteProperty, value);
    }

    private void OnItemChanged(TodoItem? oldItem, TodoItem? item)
    {
        if (oldItem != null)
            oldItem.PropertyChanged -= OnItemProperty;
        if (item != null)
            item.PropertyChanged += OnItemProperty;
        Apply();
    }

    private void OnItemProperty(object? sender, PropertyChangedEventArgs e) => Apply();

    public void Apply()
    {
        var item = Item;
        if (item == null || Store == null)
        {
            IsEnabled = false;
            return;
        }

        IsEnabled = true;
        _internal = true;
        DoneBox.IsChecked = item.Done;
        if (TextBox.Text != item.Text && !TextBox.IsKeyboardFocused)
            TextBox.Text = item.Text;
        var showIndex = Index > 0;
        IndexLabel.Text = showIndex ? Index + "." : "";
        DragHandle.Visibility = showIndex ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.ToolTip = PoolDelete ? "从待办里删除" : "从本页移除";
        var format = Store.Settings.DateFormat;
        var label = TodoDate.Format(item.Due, format);
        DateButton.Content = string.IsNullOrEmpty(label) ? "+" : label;
        DateButton.ToolTip = string.IsNullOrEmpty(label) ? "添加日期" : "修改日期";
        var tone = TodoDate.Tone(item.Due, item.Done, Store.Settings.DueSoonDays, DateTime.Today);
        Paint(item, tone);
        _internal = false;
    }

    private void Paint(TodoItem item, TodoTone tone)
    {
        var page = HostPage;
        var light = page == null ? !Dark : ColorUtil.IsLight(page.Background);
        Brush ink;
        Brush muted;
        Brush line;
        Brush fill;
        if (page != null)
        {
            ink = ColorUtil.Brush(page.FontColor, Palette.DefaultFont);
            light = ColorUtil.IsLight(page.Background);
            muted = light ? ColorUtil.Brush("#9A8F80") : ColorUtil.Brush("#8EA0BE");
            line = light ? ColorUtil.Brush("#D9CDBB") : ColorUtil.Brush("#34506A");
            fill = light ? ColorUtil.Brush("#80FFFFFF") : ColorUtil.ShiftBrush(page.Background, 0.08);
            TextBox.FontFamily = new FontFamily(FontCatalog.Normalize(page.FontFamily));
            TextBox.FontSize = page.FontSize;
            TextBox.FontWeight = page.Bold ? FontWeights.Bold : FontWeights.Normal;
        }
        else if (Dark)
        {
            ink = (Brush)FindResource("TextBrush");
            muted = (Brush)FindResource("MutedBrush");
            line = (Brush)FindResource("LineBrush");
            fill = (Brush)FindResource("SurfaceBrush");
        }
        else
        {
            ink = ColorUtil.Brush("#2A3340");
            muted = ColorUtil.Brush("#9A8F80");
            line = ColorUtil.Brush("#D9CDBB");
            fill = ColorUtil.Brush("#80FFFFFF");
        }

        if (item.Done)
        {
            ink = muted;
            TextBox.TextDecorations = TextDecorations.Strikethrough;
            Box.Opacity = 0.62;
        }
        else
        {
            TextBox.TextDecorations = null;
            Box.Opacity = 1;
        }

        Box.BorderBrush = line;
        Box.Background = fill;
        TextBox.Foreground = ink;
        TextBox.CaretBrush = ink;
        IndexLabel.Foreground = muted;
        RemoveButton.Foreground = muted;
        RemoveButton.Background = Brushes.Transparent;
        RemoveButton.BorderBrush = Brushes.Transparent;
        DateButton.Foreground = tone switch
        {
            TodoTone.Overdue => ColorUtil.Brush("#5A1E1E"),
            TodoTone.Soon => ColorUtil.Brush("#5A4A10"),
            _ => muted,
        };
        DateButton.Background = tone switch
        {
            TodoTone.Overdue => ColorUtil.Brush("#E8A0A0"),
            TodoTone.Soon => ColorUtil.Brush("#F5E6A8"),
            _ => Brushes.Transparent,
        };
        DatePopupBorder.Background = Dark ? (Brush)FindResource("SurfaceBrush") : ColorUtil.Brush("#FBF6EE");
        DatePopupBorder.BorderBrush = line;
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Item == null || Index <= 0)
            return;
        _dragArmed = true;
        _dragStart = e.GetPosition(this);
        DragHandle.CaptureMouse();
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed || Item == null)
            return;
        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4)
            return;
        _dragArmed = false;
        if (DragHandle.IsMouseCaptured)
            DragHandle.ReleaseMouseCapture();
        DragDrop.DoDragDrop(this, new DataObject(DragFormat, Item.Id), DragDropEffects.Move);
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragArmed = false;
        if (DragHandle.IsMouseCaptured)
            DragHandle.ReleaseMouseCapture();
    }

    private void Box_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragFormat) || Item == null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var source = e.Data.GetData(DragFormat) as string;
        e.Effects = source == Item.Id ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
        _restBorder ??= Box.BorderBrush;
        if (e.Effects == DragDropEffects.Move)
            Box.BorderBrush = ColorUtil.Brush("#C4A574");
    }

    private void Box_DragLeave(object sender, DragEventArgs e)
    {
        if (_restBorder != null)
            Box.BorderBrush = _restBorder;
    }

    private void Box_Drop(object sender, DragEventArgs e)
    {
        if (_restBorder != null)
            Box.BorderBrush = _restBorder;
        if (!e.Data.GetDataPresent(DragFormat) || Item == null)
            return;
        var source = e.Data.GetData(DragFormat) as string;
        if (string.IsNullOrEmpty(source) || source == Item.Id)
            return;
        TodoDropped?.Invoke(source, Item.Id);
        e.Handled = true;
    }

    private void DoneBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_internal || Item == null)
            return;
        Item.Done = DoneBox.IsChecked == true;
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e) => CommitText();

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        CommitText();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitText()
    {
        if (Item == null)
            return;
        Item.Text = TextBox.Text.Trim();
    }

    private void DateButton_Click(object sender, RoutedEventArgs e)
    {
        _picking = true;
        Picker.SelectedDate = Item?.Due;
        _picking = false;
        DatePopup.IsOpen = true;
    }

    private void Picker_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_picking || Item == null || Picker.SelectedDate == null)
            return;
        Item.Due = Picker.SelectedDate.Value.Date;
        DatePopup.IsOpen = false;
    }

    private void ClearDate_Click(object sender, RoutedEventArgs e)
    {
        if (Item == null)
            return;
        Item.Due = null;
        Picker.SelectedDate = null;
        DatePopup.IsOpen = false;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Item == null || Store == null)
            return;
        if (PoolDelete)
            Store.DeleteTodo(Item);
        else if (HostPage != null)
            Store.DetachTodo(HostPage, Item.Id);
    }
}
