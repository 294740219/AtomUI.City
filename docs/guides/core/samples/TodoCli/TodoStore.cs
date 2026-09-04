using AtomUI.City.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace TodoCli;

public sealed record TodoItem(int Id, string Title, bool IsDone);

public interface ITodoStore
{
    TodoItem Add(string title);
    IReadOnlyList<TodoItem> List();
    bool Complete(int id);
}

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(ITodoStore))]
public sealed class TodoStore : ITodoStore
{
    private readonly List<TodoItem> _items = new();
    private readonly object _gate = new();
    private int _nextId;

    public TodoItem Add(string title)
    {
        lock (_gate)
        {
            var item = new TodoItem(_nextId++, title, false);
            _items.Add(item);
            return item;
        }
    }

    public IReadOnlyList<TodoItem> List()
    {
        lock (_gate)
        {
            return _items.ToArray();
        }
    }

    public bool Complete(int id)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item is null)
            {
                return false;
            }

            var updated = item with { IsDone = true };
            _items[_items.IndexOf(item)] = updated;
            return true;
        }
    }
}
