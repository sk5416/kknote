using System.Windows;
using System.Windows.Controls;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

internal static class PageListMenu
{
    public static void Fill(ContextMenu menu, NoteStore store, Note? current)
    {
        menu.Items.Clear();
        var pages = store.DesktopPages();
        if (pages.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "还没有固化到桌面的页", IsEnabled = false });
            return;
        }

        foreach (var page in pages)
        {
            var item = new MenuItem
            {
                Header = page.Detached ? page.DisplayTitle + "  · 已拆出" : page.DisplayTitle,
                IsCheckable = true,
                IsChecked = current != null && page.Id == current.Id,
                StaysOpenOnClick = false,
                Tag = page,
            };
            var target = page;
            item.Click += (_, _) => store.RequestActivate(target);
            menu.Items.Add(item);
        }
    }
}
