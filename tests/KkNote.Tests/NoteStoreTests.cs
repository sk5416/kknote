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
    public void RoundTrip_PreservesNotesAndPlainChinese()
    {
        var path = Path.Combine(_directory, "notes.json");
        using (var store = NoteStore.Load(path))
        {
            var note = store.AddNew();
            note.Title = "会议";
            note.PinnedToDesktop = true;
            note.AlwaysOnTop = false;
            note.Background = "#102824";
            note.FontFamily = "Consolas";
            note.FontSize = 18;
            note.FontColor = "#F5D76E";
            note.Bold = true;
            note.X = 10;
            note.Y = 20;
            note.Width = 300;
            note.Height = 400;
            note.Pages[0].Text = "第一页 ☕";
            note.Pages.Add(new NotePage { Id = "p2", Text = "第二页" });
            store.SaveNow();
        }

        var json = File.ReadAllText(path);
        Assert.Contains("第一页 ☕", json);
        Assert.DoesNotContain("\\u", json);
        Assert.Contains("pinnedToDesktop", json);

        using var loaded = NoteStore.Load(path);
        var restored = Assert.Single(loaded.Notes);
        Assert.Equal("会议", restored.Title);
        Assert.True(restored.PinnedToDesktop);
        Assert.False(restored.AlwaysOnTop);
        Assert.Equal("#102824", restored.Background);
        Assert.Equal("Consolas", restored.FontFamily);
        Assert.Equal(18d, restored.FontSize);
        Assert.Equal("#F5D76E", restored.FontColor);
        Assert.True(restored.Bold);
        Assert.Equal(10d, restored.X);
        Assert.Equal(20d, restored.Y);
        Assert.Equal(300d, restored.Width);
        Assert.Equal(400d, restored.Height);
        Assert.Equal(2, restored.Pages.Count);
        Assert.Equal("第一页 ☕", restored.Pages[0].Text);
        Assert.Equal("第二页", restored.Pages[1].Text);
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
    public void MissingPagesAndTinyFont_AreRepaired()
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
        var page = Assert.Single(note.Pages);
        Assert.False(string.IsNullOrWhiteSpace(page.Id));
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
