namespace AtomUI.City.Testing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TestLayerAttribute : Attribute
{
    public TestLayerAttribute(TestLayer layer)
    {
        Layer = layer;
        Category = TestLayerNames.GetCategory(layer);
    }

    public TestLayerAttribute(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        if (!Enum.TryParse<TestLayer>(category, ignoreCase: false, out var layer)
            || !string.Equals(TestLayerNames.GetCategory(layer), category, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unknown test layer category '{category}'.", nameof(category));
        }

        Layer = layer;
        Category = category;
    }

    public TestLayer Layer { get; }

    public string Category { get; }
}
