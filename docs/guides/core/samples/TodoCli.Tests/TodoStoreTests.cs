using TodoCli;

namespace TodoCli.Tests;

public sealed class TodoStoreTests
{
    [Fact]
    public void AddThenCompleteUpdatesTheItem()
    {
        var store = new TodoStore();

        var added = store.Add("Buy milk");
        var completed = store.Complete(added.Id);

        Assert.True(completed);
        var item = Assert.Single(store.List());
        Assert.True(item.IsDone);
    }

    [Fact]
    public void CompleteUnknownIdReturnsFalse()
    {
        var store = new TodoStore();

        var completed = store.Complete(42);

        Assert.False(completed);
        Assert.Empty(store.List());
    }
}

public sealed class TodoFormatterTests
{
    [Fact]
    public void FormatRendersStatus()
    {
        var formatter = new TodoFormatter();

        var openLine = formatter.Format(new TodoItem(0, "Write docs", IsDone: false));
        var doneLine = formatter.Format(new TodoItem(1, "Ship", IsDone: true));

        Assert.Equal("[ ] #0 Write docs", openLine);
        Assert.Equal("[x] #1 Ship", doneLine);
    }
}
