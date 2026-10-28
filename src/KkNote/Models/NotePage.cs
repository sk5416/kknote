namespace KkNote.Models;

public sealed class NotePage : ObservableObject
{
    private string _id = "";
    private string _text = "";

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

    public static NotePage Create() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Text = "",
    };
}
