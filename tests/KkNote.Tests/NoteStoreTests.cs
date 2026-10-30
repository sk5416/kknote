using System.Text.Json;
using KkNote.Models;
using KkNote.Services;
using Xunit;

namespace KkNote.Tests;

public sealed class NoteStoreTests : IDisposable
{
    private readonly string _directory;

    public NoteStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "kknote-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void RoundTrip_PreservesPageTextAndPlainChinese()
    {
        var path = Path.Combine(_directory, "notes.json");
        using (var store = NoteStore.Load(path))
        {
            var note = store.AddNew();
            note.Title = "会议";
            note.Text = "第一页 ☕";
            note.PinnedToDesktop = true;
            note.AlwaysOnTop = false;
            note.Background = "#102824";
            note.FontFamily = "Consolas";
            note.FontSize = 18;
            note.FontColor = "#F5D76E";
            note.Bold = true;
            note.Detached = true;
            note.X = 10;
            note.Y = 20;
            note.Width = 300;
            note.Height = 400;
            store.SaveNow();
        }

        var json = File.ReadAllText(path);
        Assert.Contains("第一页 ☕", json);
        Assert.DoesNotContain("\\u", json);
        Assert.Contains("pinnedToDesktop", json);
        Assert.DoesNotContain("\"pages\"", json);

        using var loaded = NoteStore.Load(path);
        var restored = Assert.Single(loaded.Notes);
        Assert.Equal("会议", restored.Title);
        Assert.Equal("第一页 ☕", restored.Text);
        Assert.True(restored.PinnedToDesktop);
        Assert.False(restored.AlwaysOnTop);
        Assert.Equal("#102824", restored.Background);
        Assert.Equal("Consolas", restored.FontFamily);
        Assert.Equal(18d, restored.FontSize);
        Assert.Equal("#F5D76E", restored.FontColor);
        Assert.True(restored.Bold);
        Assert.True(restored.Detached);
        Assert.Equal(10d, restored.X);
        Assert.Equal(20d, restored.Y);
        Assert.Equal(300d, restored.Width);
        Assert.Equal(400d, restored.Height);
        Assert.Null(restored.Pages);
    }

    [Fact]
    public void CorruptFile_BacksUpAndStartsEmpty()
    {
        var path = Path.Combine(_directory, "notes.json");
        File.WriteAllText(path, "{ this is not json");

        using var store = NoteStore.Load(path);

        Assert.Empty(store.Notes);
        Assert.True(File.Exists(path + ".bak"));
        Assert.Equal("{ this is not json", File.ReadAllText(path + ".bak"));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(document.RootElement.TryGetProperty("notes", out var notes));
        Assert.Equal(JsonValueKind.Array, notes.ValueKind);
    }

    [Fact]
    public void LegacyMultiPageNote_IsFlattenedIntoPages()
    {
        var path = Path.Combine(_directory, "notes.json");
        File.WriteAllText(path, """
        {
          "settings": { "hotkey": "Ctrl+Alt+N" },
          "notes": [
            {
              "id": "old1",
              "title": "周末",
              "pinnedToDesktop": true,
              "fontSize": 14,
              "width": 280,
              "height": 340,
              "pages": [
                { "id": "p1", "text": "买菜" },
                { "id": "p2", "text": "电影" }
              ]
            }
          ]
        }
        """);

        using var store = NoteStore.Load(path);
        Assert.Equal(2, store.Notes.Count);
        Assert.Equal("周末", store.Notes[0].Title);
        Assert.Equal("买菜", store.Notes[0].Text);
        Assert.Equal("周末 · 2", store.Notes[1].Title);
        Assert.Equal("电影", store.Notes[1].Text);
        Assert.True(store.Notes[0].PinnedToDesktop);
        Assert.True(store.Notes[1].PinnedToDesktop);
        Assert.All(store.Notes, item => Assert.Null(item.Pages));
    }

    [Fact]
    public void TinyFont_IsRepaired()
    {
        var path = Path.Combine(_directory, "notes.json");
        File.WriteAllText(path, """
        {
          "settings": { "hotkey": "Ctrl+Alt+N" },
          "notes": [
            {
              "id": "",
              "title": "残缺",
              "fontSize": 9,
              "width": 10,
              "height": 10,
              "pages": []
            }
          ]
        }
        """);

        using var store = NoteStore.Load(path);
        var note = Assert.Single(store.Notes);
        Assert.False(string.IsNullOrWhiteSpace(note.Id));
        Assert.Equal(12d, note.FontSize);
        Assert.Equal(NoteLimits.MinWidth, note.Width);
        Assert.Equal(NoteLimits.MinHeight, note.Height);
        Assert.Equal("", note.Text);
    }

    [Fact]
    public void NewPage_UsesLightPaper()
    {
        var path = Path.Combine(_directory, "fresh.json");
        using var store = NoteStore.Load(path);
        var note = store.AddNew();
        Assert.Equal(Palette.DefaultBackground, note.Background);
        Assert.Equal(Palette.DefaultFont, note.FontColor);
        Assert.Equal("", note.Text);
        Assert.Equal(NoteLimits.DefaultOpacity, note.Opacity);
        Assert.False(note.Detached);
        Assert.False(store.Settings.SpreadPages);
    }

    [Fact]
    public void MissingOpacity_DefaultsToNinetyPercent()
    {
        var path = Path.Combine(_directory, "opacity.json");
        File.WriteAllText(path, """
        {
          "settings": { "hotkey": "Ctrl+Alt+N" },
          "notes": [ { "id": "a", "title": "旧", "text": "x" } ]
        }
        """);

        using var store = NoteStore.Load(path);
        var note = Assert.Single(store.Notes);
        Assert.Equal(0.9, store.Settings.Opacity);
        store.Settings.Opacity = 0.8;
        store.SaveNow();

        using var loaded = NoteStore.Load(path);
        Assert.Equal(0.8, loaded.Settings.Opacity);
    }

    [Fact]
    public void MovePageTodo_ReordersIds()
    {
        var path = Path.Combine(_directory, "order.json");
        using var store = NoteStore.Load(path);
        var a = store.AddTodo("甲");
        var b = store.AddTodo("乙");
        var c = store.AddTodo("丙");
        var page = store.AddNew(NoteKind.Todo);
        store.AttachTodo(page, a.Id);
        store.AttachTodo(page, b.Id);
        store.AttachTodo(page, c.Id);
        store.MovePageTodo(page, c.Id, a.Id);
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, page.TodoIds);
    }

    [Fact]
    public void CollapsedDesktopCard_RoundTrips()
    {
        var path = Path.Combine(_directory, "fold.json");
        using (var store = NoteStore.Load(path))
        {
            var note = store.AddNew();
            note.Collapsed = true;
            store.Settings.StackCollapsed = true;
            store.SaveNow();
        }

        using var loaded = NoteStore.Load(path);
        Assert.True(Assert.Single(loaded.Notes).Collapsed);
        Assert.True(loaded.Settings.StackCollapsed);
        Assert.Equal(NoteLimits.DefaultOpacity, loaded.Notes[0].Opacity);
    }

    [Fact]
    public void LegacySpreadPages_DetachesPinnedNotes()
    {
        var path = Path.Combine(_directory, "spread.json");
        File.WriteAllText(path, """
        {
          "settings": { "hotkey": "Ctrl+Alt+N", "spreadPages": true },
          "notes": [
            { "id": "a", "title": "A", "text": "a", "pinnedToDesktop": true },
            { "id": "b", "title": "B", "text": "b", "pinnedToDesktop": false }
          ]
        }
        """);

        using var store = NoteStore.Load(path);
        Assert.True(store.Notes[0].Detached);
        Assert.False(store.Notes[1].Detached);
        Assert.False(store.Settings.SpreadPages);
    }

    [Fact]
    public void RoundTrip_PreservesTodosAndPageRefs()
    {
        var path = Path.Combine(_directory, "todos.json");
        string todoId;
        using (var store = NoteStore.Load(path))
        {
            var todo = store.AddTodo("买菜", new DateTime(2026, 9, 18));
            todoId = todo.Id;
            var page = store.AddNew(NoteKind.Todo);
            page.Title = "本周";
            store.AttachTodo(page, todo.Id);
            store.Settings.DateFormat = DateFormats.Ymd;
            store.Settings.DueSoonDays = 5;
            store.SaveNow();
        }

        var json = File.ReadAllText(path);
        Assert.Contains("买菜", json);
        Assert.Contains("2026-09-18", json);
        Assert.Contains("\"kind\": \"todo\"", json);

        using var loaded = NoteStore.Load(path);
        var restored = Assert.Single(loaded.Todos);
        Assert.Equal("买菜", restored.Text);
        Assert.Equal(new DateTime(2026, 9, 18), restored.Due);
        Assert.Equal(DateFormats.Ymd, loaded.Settings.DateFormat);
        Assert.Equal(5, loaded.Settings.DueSoonDays);
        var pageRestored = Assert.Single(loaded.Notes);
        Assert.True(pageRestored.IsTodo);
        Assert.Equal(todoId, Assert.Single(pageRestored.TodoIds));
        Assert.Equal("待办 · 1 项", pageRestored.DisplaySnippet);
    }

    [Fact]
    public void DeleteTodo_StripsPageRefs()
    {
        var path = Path.Combine(_directory, "strip.json");
        using var store = NoteStore.Load(path);
        var todo = store.AddTodo("电影");
        var page = store.AddNew(NoteKind.Todo);
        store.AttachTodo(page, todo.Id);
        store.DeleteTodo(todo);
        Assert.Empty(store.Todos);
        Assert.Empty(page.TodoIds);
    }

    [Fact]
    public void LegacyJson_WithoutKind_IsTextPage()
    {
        var path = Path.Combine(_directory, "legacy-kind.json");
        File.WriteAllText(path, """
        {
          "settings": { "hotkey": "Ctrl+Alt+N" },
          "notes": [ { "id": "a", "title": "旧", "text": "hello" } ]
        }
        """);

        using var store = NoteStore.Load(path);
        var note = Assert.Single(store.Notes);
        Assert.Equal(NoteKind.Text, note.Kind);
        Assert.False(note.IsTodo);
        Assert.Empty(store.Todos);
        Assert.Equal(DateFormats.Md, store.Settings.DateFormat);
        Assert.Equal(3, store.Settings.DueSoonDays);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
