using AtomUI.City.Generators.Diagnostics;
using AtomUI.City.Generators.Routing;

namespace AtomUI.City.Generators.Tests;

public sealed class RouteManifestBuilderTests
{
    private const string DefaultViewModelTypeName = "__default_view_model__";

    [Fact]
    public void BuildSortsRoutesDeterministicallyAndResolvesParentRouteIds()
    {
        var result = RouteManifestBuilder.Build(
            [
                Route("Sample.App.AppRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "settings", parentMethodName: "Shell"),
                Route("Sample.App.AppRoutes", "Shell", "app.shell", RouteDefinitionMetadataKind.Layout, null),
            ]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(["app.settings", "app.shell"], result.Manifest.Routes.Select(route => route.Id));
        Assert.Equal("app.shell", result.Manifest.Routes[0].ParentRouteId);
    }

    [Fact]
    public void BuildReportsDuplicateRouteIds()
    {
        var result = RouteManifestBuilder.Build(
            [
                Route("Sample.App.AppRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "settings"),
                Route("Sample.App.AdminRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "admin/settings"),
            ]);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.DuplicateRoute, diagnostic.Id);
        Assert.Empty(result.Manifest.Routes);
    }

    [Fact]
    public void BuildReportsMissingParentRoutes()
    {
        var result = RouteManifestBuilder.Build(
            [
                Route("Sample.App.AppRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "settings", parentMethodName: "Shell"),
            ]);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Empty(result.Manifest.Routes);
    }

    [Fact]
    public void BuildReportsSiblingTemplateConflicts()
    {
        var result = RouteManifestBuilder.Build(
            [
                Route("Sample.App.AppRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "settings", parentMethodName: "Shell"),
                Route("Sample.App.AppRoutes", "Profile", "app.profile", RouteDefinitionMetadataKind.Route, "settings", parentMethodName: "Shell"),
                Route("Sample.App.AppRoutes", "Shell", "app.shell", RouteDefinitionMetadataKind.Layout, null),
            ]);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.DuplicateRoute, diagnostic.Id);
        Assert.Empty(result.Manifest.Routes);
    }

    [Fact]
    public void BuildUsesStructuredOrdinalParentAndOutletIdentity()
    {
        var result = RouteManifestBuilder.Build(
        [
            Route(
                "Sample.App.AppRoutes",
                "Parent",
                "Parent",
                RouteDefinitionMetadataKind.Group,
                "upper",
                viewModelTypeName: null),
            Route(
                "Sample.App.AppRoutes",
                "parent",
                "parent",
                RouteDefinitionMetadataKind.Group,
                "lower",
                viewModelTypeName: null),
            new RouteDefinitionMetadata(
                "Sample.App.AppRoutes",
                "UpperItem",
                "upper-item",
                RouteDefinitionMetadataKind.Route,
                "item",
                "Sample.App.ViewModel",
                "Parent",
                "Side",
                null,
                null),
            new RouteDefinitionMetadata(
                "Sample.App.AppRoutes",
                "LowerItem",
                "lower-item",
                RouteDefinitionMetadataKind.Route,
                "item",
                "Sample.App.ViewModel",
                "parent",
                "side",
                null,
                null),
        ]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(4, result.Manifest.Routes.Count);
    }

    [Fact]
    public void BuildReportsInvalidRouteKindCombinations()
    {
        var result = RouteManifestBuilder.Build(
        [
            Route("Sample.App.AppRoutes", "MissingTemplate", "missing-template", RouteDefinitionMetadataKind.Route, null),
            new RouteDefinitionMetadata(
                "Sample.App.AppRoutes",
                "InvalidGroup",
                "invalid-group",
                RouteDefinitionMetadataKind.Group,
                null,
                "Sample.App.ViewModel",
                null,
                "primary",
                null,
                null),
        ]);

        Assert.Empty(result.Manifest.Routes);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Routes require a template", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Route groups require a template", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildReportsInvalidTemplatesAndMissingRouteTargets()
    {
        var result = RouteManifestBuilder.Build(
            [
                Route("Sample.App.AppRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "settings/{id", viewModelTypeName: null),
            ]);

        Assert.Collection(
            result.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
                Assert.Contains("requires a ViewModel target", diagnostic.Message, StringComparison.Ordinal);
                Assert.Equal("app.settings", diagnostic.Target);
            },
            diagnostic =>
            {
                Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
                Assert.Contains("invalid route template", diagnostic.Message, StringComparison.Ordinal);
                Assert.Equal("app.settings", diagnostic.Target);
            });
        Assert.Empty(result.Manifest.Routes);
    }

    [Fact]
    public void BuildCarriesRouteLocalizationMetadata()
    {
        var result = RouteManifestBuilder.Build(
            [
                new RouteDefinitionMetadata(
                    "Sample.App.AppRoutes",
                    "Settings",
                    "app.settings",
                    RouteDefinitionMetadataKind.Route,
                    "settings",
                    "Sample.App.SettingsViewModel",
                    parentMethodName: null,
                    outletName: "primary",
                    extensionPoint: null,
                    redirectTargetMethodName: null,
                    titleKey: "Routes.Settings.Title",
                    descriptionKey: "Routes.Settings.Description",
                    breadcrumbKey: "Routes.Settings.Breadcrumb",
                    groupKey: "Routes.Settings.Group",
                    errorTitleKey: "Routes.Settings.ErrorTitle"),
            ]);

        var route = Assert.Single(result.Manifest.Routes);

        Assert.Equal("Routes.Settings.Title", route.TitleKey);
        Assert.Equal("Routes.Settings.Description", route.DescriptionKey);
        Assert.Equal("Routes.Settings.Breadcrumb", route.BreadcrumbKey);
        Assert.Equal("Routes.Settings.Group", route.GroupKey);
        Assert.Equal("Routes.Settings.ErrorTitle", route.ErrorTitleKey);
    }

    [Fact]
    public void BuildReturnsReadonlyRouteManifestCollections()
    {
        var result = RouteManifestBuilder.Build(
            [
                Route("Sample.App.AppRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "settings"),
            ]);
        var routes = Assert.IsAssignableFrom<IList<RouteManifestRoute>>(result.Manifest.Routes);
        var diagnostics = Assert.IsAssignableFrom<IList<GeneratorDiagnostic>>(result.Diagnostics);

        Assert.Throws<NotSupportedException>(() => routes[0] = new RouteManifestRoute("changed", RouteDefinitionMetadataKind.Route, "changed", "ChangedViewModel", null, "primary", null, null));
        Assert.Throws<NotSupportedException>(() => diagnostics.Add(new GeneratorDiagnostic(GeneratorDiagnostics.InvalidManifestInput, "Changed")));
        Assert.Equal("app.settings", result.Manifest.Routes[0].Id);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void RouteMapMetadataRoutesRejectExternalMutation()
    {
        var routeList = new List<RouteDefinitionMetadata>
        {
            Route("Sample.App.AppRoutes", "Settings", "app.settings", RouteDefinitionMetadataKind.Route, "settings"),
        };
        var metadata = new RouteMapMetadata("Sample.App.AppRoutes", routeList);
        var routes = Assert.IsAssignableFrom<IList<RouteDefinitionMetadata>>(metadata.Routes);

        Assert.Throws<NotSupportedException>(() => routes[0] = Route("Sample.App.AppRoutes", "Other", "app.other", RouteDefinitionMetadataKind.Route, "other"));
        Assert.Equal("app.settings", metadata.Routes[0].Id);
    }

    private static RouteDefinitionMetadata Route(
        string routeMapTypeName,
        string methodName,
        string id,
        RouteDefinitionMetadataKind kind,
        string? template,
        string? parentMethodName = null,
        string? viewModelTypeName = DefaultViewModelTypeName)
    {
        return new RouteDefinitionMetadata(
            routeMapTypeName,
            methodName,
            id,
            kind,
            template,
            string.Equals(viewModelTypeName, DefaultViewModelTypeName, StringComparison.Ordinal)
                ? "Sample.App." + methodName + "ViewModel"
                : viewModelTypeName,
            parentMethodName,
            outletName: "primary",
            extensionPoint: null,
            redirectTargetMethodName: null);
    }
}
