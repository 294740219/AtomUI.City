using AtomUI.City.Testing;

namespace AtomUI.City.Testing.Tests;

public sealed class TestLayerTests
{
    [Fact]
    public void TestLayerNamesExposeStableCategoryValues()
    {
        Assert.Equal("Unit", TestLayerNames.GetCategory(TestLayer.Unit));
        Assert.Equal("Contract", TestLayerNames.GetCategory(TestLayer.Contract));
        Assert.Equal("FrameworkIntegration", TestLayerNames.GetCategory(TestLayer.FrameworkIntegration));
        Assert.Equal("RuntimeLifecycle", TestLayerNames.GetCategory(TestLayer.RuntimeLifecycle));
        Assert.Equal("PluginLifecycle", TestLayerNames.GetCategory(TestLayer.PluginLifecycle));
        Assert.Equal("PlatformIntegration", TestLayerNames.GetCategory(TestLayer.PlatformIntegration));
        Assert.Equal("TemplateSmoke", TestLayerNames.GetCategory(TestLayer.TemplateSmoke));
        Assert.Equal("Generator", TestLayerNames.GetCategory(TestLayer.Generator));
        Assert.Equal("Analyzer", TestLayerNames.GetCategory(TestLayer.Analyzer));
        Assert.Equal("Build", TestLayerNames.GetCategory(TestLayer.Build));
    }

    [Fact]
    public void TestLayerAttributeStoresRunnerNeutralMetadata()
    {
        var attribute = new TestLayerAttribute(TestLayer.FrameworkIntegration);

        Assert.Equal(TestLayer.FrameworkIntegration, attribute.Layer);
        Assert.Equal("FrameworkIntegration", attribute.Category);
    }

    [Fact]
    public void TestLayerNamesExposeImmutableStandardCategorySet()
    {
        Assert.Contains(TestLayerNames.PluginLifecycle, TestLayerNames.AllCategories);
        Assert.Contains(TestLayerNames.TemplateSmoke, TestLayerNames.AllCategories);
        Assert.True(TestLayerNames.IsKnownCategory(TestLayerNames.Generator));
        Assert.False(TestLayerNames.IsKnownCategory("Unknown"));

        var categories = Assert.IsAssignableFrom<IList<string>>(TestLayerNames.AllCategories);

        Assert.Throws<NotSupportedException>(() => categories.Add("Unknown"));
    }

    [Fact]
    public void TestLayerAttributeAcceptsKnownCategoryAndRejectsUnknownCategory()
    {
        var attribute = new TestLayerAttribute(TestLayerNames.PluginLifecycle);

        Assert.Equal(TestLayer.PluginLifecycle, attribute.Layer);
        Assert.Equal(TestLayerNames.PluginLifecycle, attribute.Category);
        Assert.Throws<ArgumentException>(() => new TestLayerAttribute("Unknown"));
    }
}
