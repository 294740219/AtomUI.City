using Microsoft.Extensions.Options;

namespace TodoCli;

public sealed class TodoOptions
{
    public string? DefaultTitle { get; set; }
}

public sealed class TodoOptionsWriter
    : IConfigureOptions<TodoOptions>
{
    public void Configure(TodoOptions options)
    {
        options.DefaultTitle ??= "untitled";
    }
}
