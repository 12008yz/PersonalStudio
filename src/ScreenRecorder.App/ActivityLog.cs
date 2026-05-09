using System.Collections.ObjectModel;

namespace ScreenRecorder_App;

/// <summary>Простая лента сообщений для пользователя и отладки (фаза A).</summary>
public sealed class ActivityLog
{
    public ObservableCollection<string> Entries { get; } = new();
}
