using AtomUI.City.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace TodoCli;

public interface ITodoFormatter
{
    string Format(TodoItem item);
}

[Service(ServiceLifetime.Transient)]
[ExposeServices(typeof(ITodoFormatter))]
public sealed class TodoFormatter : ITodoFormatter
{
    public string Format(TodoItem item)
    {
        return $"[{(item.IsDone ? "x" : " ")}] #{item.Id} {item.Title}";
    }
}
