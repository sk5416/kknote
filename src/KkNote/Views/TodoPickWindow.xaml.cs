using System.Windows;
using System.Windows.Controls;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class TodoPickWindow : Window
{
    private readonly NoteStore _store;
    private readonly Note _page;

    public TodoPickWindow(NoteStore store, Note page)
    {
        _store = store;
        _page = page;
        InitializeComponent();
        var onPage = page.TodoIds.ToHashSet();
        var candidates = store.Todos.Where(item => !item.Done && !onPage.Contains(item.Id)).ToList();
        EmptyHint.Visibility = candidates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in candidates)
        {
            var date = TodoDate.Format(item.Due, store.Settings.DateFormat);
            PickList.Items.Add(new ListBoxItem
            {
                Content = string.IsNullOrEmpty(date) ? item.Text : $"{item.Text}  {date}",
                Tag = item,
            });
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowEffects.ApplyRoundedDark(this);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        foreach (ListBoxItem row in PickList.SelectedItems)
        {
            if (row.Tag is TodoItem item)
                _store.AttachTodo(_page, item.Id);
        }

        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
