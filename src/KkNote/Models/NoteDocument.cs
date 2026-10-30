namespace KkNote.Models;

public sealed class NoteDocument
{
    public AppSettings Settings { get; set; } = new();
    public List<Note> Notes { get; set; } = [];
    public List<TodoItem> Todos { get; set; } = [];
}
