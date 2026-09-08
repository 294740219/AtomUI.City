using AtomUI.City.Routing;

namespace AtomUI.City.Routing.Tests;

public sealed class RouteTemplateTests
{
    [Theory]
    [InlineData("orders/{id:min(10)}", "orders/10", true)]
    [InlineData("orders/{id:min(10)}", "orders/9", false)]
    [InlineData("orders/{id:max(10)}", "orders/11", false)]
    [InlineData("orders/{id:range(10,20)}", "orders/15", true)]
    [InlineData("code/{value:length(4)}", "code/1234", true)]
    [InlineData("code/{value:minlength(3):maxlength(5)}", "code/123456", false)]
    [InlineData("slug/{value:regex(^[a-z]+$)}", "slug/atom", true)]
    [InlineData("slug/{value:regex(^[a-z]+$)}", "slug/123", false)]
    public void BuiltInArgumentConstraintsAreEnforced(string pattern, string path, bool expected)
    {
        var template = RouteTemplate.Parse(pattern);

        Assert.Equal(expected, template.TryMatch(path, out _));
    }

    [Theory]
    [InlineData("orders/{id:range(20,10)}")]
    [InlineData("orders/{id:min(nope)}")]
    [InlineData("orders/{id:length(-1)}")]
    [InlineData("orders/{id:regex([)}")]
    public void InvalidConstraintArgumentsAreRejectedWhenTemplateIsParsed(string pattern)
    {
        Assert.Throws<RouteGraphException>(() => RouteTemplate.Parse(pattern));
    }

    [Fact]
    public void EncodedSlashRemainsInsideOneCapturedPathSegment()
    {
        var template = RouteTemplate.Parse("files/{name}");

        var matched = template.TryMatch("files/a%2Fb", out var parameters);

        Assert.True(matched);
        Assert.Equal("a/b", parameters["name"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void ParseSupportsRootTemplate(string pattern)
    {
        var template = RouteTemplate.Parse(pattern);

        Assert.Equal(string.Empty, template.Pattern);
        Assert.Empty(template.Segments);
        Assert.True(template.TryMatch("/", out var values));
        Assert.Empty(values);
    }

    [Fact]
    public void ParseSupportsAspNetStyleTemplateSegments()
    {
        var template = RouteTemplate.Parse("items/{id:int}/{slug=overview}/files/{*path}");

        Assert.Equal("items/{id:int}/{slug=overview}/files/{*path}", template.Pattern);
        Assert.Collection(
            template.Segments,
            segment => Assert.Equal(RouteTemplateSegmentKind.Literal, segment.Kind),
            segment =>
            {
                Assert.Equal(RouteTemplateSegmentKind.Parameter, segment.Kind);
                Assert.Equal("id", segment.Name);
                Assert.Equal(["int"], segment.Constraints);
            },
            segment =>
            {
                Assert.Equal(RouteTemplateSegmentKind.Parameter, segment.Kind);
                Assert.Equal("slug", segment.Name);
                Assert.Equal("overview", segment.DefaultValue);
            },
            segment => Assert.Equal(RouteTemplateSegmentKind.Literal, segment.Kind),
            segment =>
            {
                Assert.Equal(RouteTemplateSegmentKind.CatchAll, segment.Kind);
                Assert.Equal("path", segment.Name);
            });
    }

    [Fact]
    public void TemplateCollectionsRejectExternalListMutation()
    {
        var template = RouteTemplate.Parse("items/{id:int}");
        var replacement = RouteTemplate.Parse("replacement").Segments[0];
        var segments = Assert.IsAssignableFrom<IList<RouteTemplateSegment>>(template.Segments);
        var constraints = Assert.IsAssignableFrom<IList<string>>(template.Segments[1].Constraints);

        Assert.Throws<NotSupportedException>(() => segments[0] = replacement);
        Assert.Throws<NotSupportedException>(() => constraints[0] = "guid");
        Assert.Equal(RouteTemplateSegmentKind.Literal, template.Segments[0].Kind);
        Assert.Equal("int", template.Segments[1].Constraints[0]);
    }

    [Theory]
    [InlineData("items/{id}/{id}")]
    [InlineData("docs/{*path}/edit")]
    [InlineData("items/{id:unknown}")]
    [InlineData("settings/{id")]
    [InlineData("settings/id}")]
    public void ParseRejectsInvalidTemplateSyntax(string pattern)
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteTemplate.Parse(pattern));

        Assert.Equal(RouteGraphError.InvalidRouteTemplate, exception.Error);
    }

    [Fact]
    public void TryMatchExtractsParametersAndAppliesConstraints()
    {
        var template = RouteTemplate.Parse("orders/{id:int}/items/{itemId:guid}");
        var matched = template.TryMatch("orders/42/items/6f9619ff-8b86-d011-b42d-00cf4fc964ff", out var values);
        var rejected = template.TryMatch("orders/not-an-int/items/6f9619ff-8b86-d011-b42d-00cf4fc964ff", out _);

        Assert.True(matched);
        Assert.False(rejected);
        Assert.Equal("42", values["id"]);
        Assert.Equal("6f9619ff-8b86-d011-b42d-00cf4fc964ff", values["itemId"]);
    }

    [Fact]
    public void TryMatchSupportsOptionalDefaultAndCatchAllSegments()
    {
        var template = RouteTemplate.Parse("docs/{lang=en}/{*path}");

        Assert.True(template.TryMatch("docs", out var defaultValues));
        Assert.Equal("en", defaultValues["lang"]);

        Assert.True(template.TryMatch("docs/zh-CN/guides/getting-started", out var values));
        Assert.Equal("zh-CN", values["lang"]);
        Assert.Equal("guides/getting-started", values["path"]);
    }

    [Fact]
    public void TryMatchRejectsNullPath()
    {
        var template = RouteTemplate.Parse("docs/{id:int}");

        Assert.Throws<ArgumentNullException>(() => template.TryMatch(null!, out _));
    }

    [Fact]
    public void TryMatchReturnsReadonlyParameterDictionary()
    {
        var template = RouteTemplate.Parse("profile/{id:int}");

        Assert.True(template.TryMatch("profile/42", out var values));
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, string>>(values);

        Assert.Throws<NotSupportedException>(() => dictionary["id"] = "99");
    }
}
