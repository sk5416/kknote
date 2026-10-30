using System.Text.Json.Serialization;

namespace KkNote.Models;

public sealed class TodoItem : ObservableObject
{
    private string _id = "";
    private string _text = "";
    private DateTime? _due;
    private bool _done;
    private DateTime _createdAt = DateTime.Now;

    public string Id
    {
        get => _id;
        set => Set(ref _id, value);
    }

    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? "");
    }

    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTime? Due
    {
        get => _due;
        set => Set(ref _due, value?.Date);
    }

    public bool Done
    {
        get => _done;
        set => Set(ref _done, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => Set(ref _createdAt, value);
    }

    public static TodoItem Create(string text, DateTime? due = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Text = text ?? "",
        Due = due?.Date,
        CreatedAt = DateTime.Now,
    };
}
