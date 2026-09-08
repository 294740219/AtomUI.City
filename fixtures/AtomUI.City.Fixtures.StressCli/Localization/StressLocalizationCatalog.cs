using System.Globalization;
using AtomUI.City.Localization;

namespace AtomUI.City.Fixtures.StressCli.Localization;

public static class StressLocalizationCatalog
{
    public const int DescriptorCount = 30;
    public const int MinimumDistinctKeyCount = 120;
    public const int MinimumResourceEntryCount = 250;

    public const string OperationsModuleId = "fixtures.module.operations";
    public const string BillingModuleId = "fixtures.module.billing";
    public const string SupportModuleId = "fixtures.module.support";
    public const string OrdersRouteId = "fixtures.routes.orders";
    public const string PaymentsRouteId = "fixtures.routes.payments";
    public const string SearchRouteId = "fixtures.routes.search";
    public const string SupportRouteId = "fixtures.routes.support";
    public const string ReportsRouteId = "fixtures.routes.reports";
    public const string MainWindowId = "fixtures.window.main";
    public const string ExportWindowId = "fixtures.window.export";
    public const string SalesPluginId = "fixtures.plugin.sales";
    public const string SalesContributionId = "fixtures.plugin.sales.localization";

    private static readonly TextEntry[] HostTexts =
    [
        new("Common.Ok", "OK", "确定"),
        new("Common.Cancel", "Cancel", "取消"),
        new("Common.Save", "Save", "保存"),
        new("Common.Delete", "Delete", "删除"),
        new("Common.Search", "Search", "搜索"),
        new("Common.Refresh", "Refresh", "刷新"),
        new("Common.Export", "Export", "导出"),
        new("Common.Import", "Import", "导入"),
        new("Common.Yes", "Yes", "是"),
        new("Common.No", "No", "否"),
        new("Menu.Dashboard", "Dashboard", "仪表盘"),
        new("Menu.Orders", "Orders", "订单"),
        new("Menu.Inventory", "Inventory", "库存"),
        new("Menu.Customers", "Customers", "客户"),
        new("Menu.Billing", "Billing", "账单"),
        new("Menu.Payments", "Payments", "支付"),
        new("Menu.Reports", "Reports", "报表"),
        new("Menu.Support", "Support", "支持"),
        new("Menu.Settings", "Settings", "设置"),
        new("Menu.SignOut", "Sign out", "退出登录"),
        new("Status.Loading", "Loading", "加载中"),
        new("Status.Ready", "Ready", "就绪"),
        new("Status.Online", "Online", "在线"),
        new("Status.Offline", "Offline", "离线"),
        new("Status.Processing", "Processing", "处理中"),
        new("Status.Completed", "Completed", "已完成"),
        new("Errors.Unknown", "An unknown error occurred.", "发生未知错误。"),
        new("Errors.Network", "The network is unavailable.", "网络不可用。"),
        new("Errors.Unauthorized", "You are not authorized.", "你没有操作权限。"),
        new("Errors.Validation", "Some fields are invalid.", "部分字段无效。"),
    ];

    private static readonly TextEntry[] PresentationTexts =
    [
        new("Common.Save", "Save presentation", "保存界面"),
        new("Presentation.Theme", "System theme", "系统主题"),
        new("Presentation.Navigation", "Navigation", "导航"),
        new("Presentation.Dialog", "Dialog", "对话框"),
        new("Presentation.NotificationArea", "Notification area", "通知区域"),
        new("Presentation.CommandPalette", "Command palette", "命令面板"),
        new("Presentation.PrimaryWindow", "Primary window", "主窗口"),
        new("Presentation.SecondaryWindow", "Secondary window", "辅助窗口"),
        new("Presentation.LayoutDirection", "Left to right", "从左到右"),
        new("Presentation.FocusMode", "Focus mode", "专注模式"),
        new("Presentation.Accessibility", "Accessibility", "辅助功能"),
    ];

    private static readonly TextEntry[] OperationsTexts =
    [
        new("Operations.Title", "Operations center", "运营中心"),
        new("Operations.Description", "Coordinate daily commerce operations.", "协调日常商业运营。"),
        new("Operations.Queue", "Work queue", "工作队列"),
        new("Operations.Batch", "Batch operation", "批量操作"),
        new("Operations.Assign", "Assign operator", "分配操作员"),
        new("Operations.Escalate", "Escalate", "升级处理"),
        new("Operations.Audit", "Audit trail", "审计记录"),
        new("Operations.TaskRunning", "Task {0} is running.", "任务 {0} 正在运行。"),
        new("Operations.TaskCompleted", "Task {0} completed in {1:N0} ms.", "任务 {0} 已在 {1:N0} 毫秒内完成。"),
        new("Operations.SelectionCount", "{0:N0} items selected.", "已选择 {0:N0} 项。"),
        new("Operations.Owner", "Current owner: {0}", "当前负责人：{0}"),
        new("Operations.LastUpdated", "Updated at {0:t}", "更新于 {0:t}"),
        new("Common.Save", "Save operation", "保存操作"),
        new("Common.Export", "Export operations", "导出运营数据"),
    ];

    private static readonly TextEntry[] BillingTexts =
    [
        new("Billing.Title", "Billing center", "账单中心"),
        new("Billing.Invoice", "Invoice", "发票"),
        new("Billing.Settlement", "Settlement", "结算"),
        new("Billing.Tax", "Tax", "税额"),
        new("Billing.Refund", "Refund", "退款"),
        new("Billing.Overdue", "Overdue", "逾期"),
        new("Billing.Paid", "Paid", "已支付"),
        new("Billing.Pending", "Pending payment", "待支付"),
        new("Billing.Amount", "Amount: {0:N2}", "金额：{0:N2}"),
        new("Billing.InvoiceNumber", "Invoice {0}", "发票 {0}"),
        new("Billing.PaymentReceived", "Received {0:N2} from {1}.", "已收到来自 {1} 的 {0:N2}。"),
        new("Billing.ClosePeriod", "Close accounting period", "关闭会计期间"),
    ];

    private static readonly TextEntry[] SupportTexts =
    [
        new("Support.Title", "Support desk", "客服中心"),
        new("Support.OpenTicket", "Open ticket", "新建工单"),
        new("Support.Assign", "Assign ticket", "分配工单"),
        new("Support.Reply", "Reply", "回复"),
        new("Support.Close", "Close ticket", "关闭工单"),
        new("Support.Priority.High", "High priority", "高优先级"),
        new("Support.Priority.Normal", "Normal priority", "普通优先级"),
        new("Support.Sla", "SLA remaining: {0:g}", "SLA 剩余：{0:g}"),
        new("Support.AssignedTo", "Assigned to {0}", "已分配给 {0}"),
        new("Support.WaitingCustomer", "Waiting for customer", "等待客户回复"),
    ];

    private static readonly TextEntry[] MainWindowTexts =
    [
        new("MainWindow.Title", "AtomUI City Operations", "AtomUI City 运营控制台"),
        new("MainWindow.StatusBar", "Operations are healthy.", "运营状态正常。"),
        new("MainWindow.OpenWorkspace", "Open workspace", "打开工作区"),
        new("MainWindow.CloseWorkspace", "Close workspace", "关闭工作区"),
        new("MainWindow.SwitchTenant", "Switch tenant", "切换租户"),
        new("MainWindow.UserMenu", "User menu", "用户菜单"),
        new("MainWindow.Shortcuts", "Keyboard shortcuts", "键盘快捷键"),
        new("Common.Save", "Save workspace", "保存工作区"),
    ];

    private static readonly TextEntry[] ExportWindowTexts =
    [
        new("ExportWindow.Title", "Export data", "导出数据"),
        new("ExportWindow.Format", "File format", "文件格式"),
        new("ExportWindow.Range", "Date range", "日期范围"),
        new("ExportWindow.Columns", "Columns", "列"),
        new("ExportWindow.IncludeHeaders", "Include headers", "包含表头"),
        new("ExportWindow.Compress", "Compress output", "压缩输出"),
        new("ExportWindow.Start", "Start export", "开始导出"),
        new("Common.Save", "Save export preset", "保存导出预设"),
    ];

    private static readonly TextEntry[] SalesPluginTexts =
    [
        new("SalesPlugin.Title", "Sales intelligence", "销售智能"),
        new("SalesPlugin.Banner", "Sales extension is active.", "销售扩展已启用。"),
        new("SalesPlugin.Forecast", "Sales forecast", "销售预测"),
        new("SalesPlugin.Commission", "Commission", "佣金"),
        new("SalesPlugin.Target", "Target: {0:N2}", "目标：{0:N2}"),
        new("SalesPlugin.Achievement", "Achievement: {0:P1}", "完成率：{0:P1}"),
        new("SalesPlugin.TopRegion", "Top region: {0}", "领先区域：{0}"),
        new("SalesPlugin.Refresh", "Refresh sales data", "刷新销售数据"),
        new("SalesPlugin.Disabled", "Sales extension is disabled.", "销售扩展已停用。"),
        new("Common.Export", "Export sales report", "导出销售报表"),
    ];

    public static IReadOnlyList<LanguagePackageDescriptor> CreateDescriptors()
    {
        var descriptors = new List<LanguagePackageDescriptor>(DescriptorCount);

        AddPrimaryPair(descriptors, "Host.Core", ResourceScope.Host, null, null, HostTexts, ["Common.Ok"]);
        AddNeutral(descriptors, "Host.Core", "en", ResourceScope.Host, null, ("Host.ParentOnly", "Neutral English fallback"));
        AddNeutral(descriptors, "Host.Core", "zh-Hans", ResourceScope.Host, null, ("Host.ParentOnly", "简体中文回退文案"));

        AddPrimaryPair(descriptors, "Presentation.Shell", ResourceScope.Presentation, null, null, PresentationTexts);

        AddPrimaryPair(descriptors, "Module.Operations", ResourceScope.Module, OperationsModuleId, null, OperationsTexts, ["Operations.Title"]);
        AddNeutral(descriptors, "Module.Operations", "en", ResourceScope.Module, OperationsModuleId, ("Operations.LegacyHint", "Open the legacy operations panel."));
        AddNeutral(descriptors, "Module.Operations", "zh-Hans", ResourceScope.Module, OperationsModuleId, ("Operations.LegacyHint", "打开旧版运营面板。"));

        AddPrimaryPair(descriptors, "Module.Billing", ResourceScope.Module, BillingModuleId, null, BillingTexts);
        AddPrimaryPair(descriptors, "Module.Support", ResourceScope.Module, SupportModuleId, null, SupportTexts);

        AddPrimaryPair(descriptors, "Route.Orders", ResourceScope.Route, OrdersRouteId, null, CreateRouteTexts("Orders", "Orders", "订单"));
        AddPrimaryPair(descriptors, "Route.Payments", ResourceScope.Route, PaymentsRouteId, null, CreateRouteTexts("Payments", "Payments", "支付"));
        AddPrimaryPair(descriptors, "Route.Search", ResourceScope.Route, SearchRouteId, null, CreateRouteTexts("Search", "Search", "搜索"));
        AddPrimaryPair(descriptors, "Route.Support", ResourceScope.Route, SupportRouteId, null, CreateRouteTexts("SupportRoute", "Support", "支持"));
        AddPrimaryPair(descriptors, "Route.Reports", ResourceScope.Route, ReportsRouteId, null, CreateRouteTexts("Reports", "Reports", "报表"));

        AddPrimaryPair(descriptors, "Window.Main", ResourceScope.Window, MainWindowId, null, MainWindowTexts);
        AddPrimaryPair(descriptors, "Window.Export", ResourceScope.Window, ExportWindowId, null, ExportWindowTexts);
        AddPrimaryPair(descriptors, "Plugin.Sales", ResourceScope.Plugin, SalesPluginId, SalesContributionId, SalesPluginTexts);

        return Array.AsReadOnly(descriptors.ToArray());
    }

    private static TextEntry[] CreateRouteTexts(string prefix, string englishName, string chineseName)
    {
        return
        [
            new($"{prefix}.Title", englishName, chineseName),
            new($"{prefix}.Description", $"Manage {englishName.ToLowerInvariant()} business data.", $"管理{chineseName}业务数据。"),
            new($"{prefix}.Empty", $"No {englishName.ToLowerInvariant()} data.", $"暂无{chineseName}数据。"),
            new($"{prefix}.Filter.All", "All records", "全部记录"),
            new($"{prefix}.Action.Open", $"Open {englishName.ToLowerInvariant()}", $"打开{chineseName}"),
            new($"{prefix}.Status.Ready", $"{englishName} are ready.", $"{chineseName}已就绪。"),
            new($"{prefix}.Error.Load", $"Could not load {englishName.ToLowerInvariant()}.", $"无法加载{chineseName}。"),
            new("Route.ContextMarker", $"{englishName} route", $"{chineseName}路由"),
            new("Common.Save", $"Save {englishName.ToLowerInvariant()}", $"保存{chineseName}"),
        ];
    }

    private static void AddPrimaryPair(
        ICollection<LanguagePackageDescriptor> descriptors,
        string packageId,
        ResourceScope scope,
        string? scopeId,
        string? contributionId,
        IReadOnlyList<TextEntry> texts,
        IReadOnlyList<string>? criticalKeys = null)
    {
        AddDescriptor(descriptors, packageId, "en-US", "en", scope, scopeId, contributionId, ToResources(texts, english: true), criticalKeys);
        AddDescriptor(descriptors, packageId, "zh-CN", "zh-Hans", scope, scopeId, contributionId, ToResources(texts, english: false), criticalKeys);
    }

    private static void AddNeutral(
        ICollection<LanguagePackageDescriptor> descriptors,
        string packageId,
        string cultureName,
        ResourceScope scope,
        string? scopeId,
        params (string Key, string Value)[] resources)
    {
        AddDescriptor(
            descriptors,
            packageId,
            cultureName,
            fallbackCultureName: null,
            scope,
            scopeId,
            contributionId: null,
            resources.ToDictionary(resource => resource.Key, resource => resource.Value, StringComparer.Ordinal));
    }

    private static void AddDescriptor(
        ICollection<LanguagePackageDescriptor> descriptors,
        string packageId,
        string cultureName,
        string? fallbackCultureName,
        ResourceScope scope,
        string? scopeId,
        string? contributionId,
        IReadOnlyDictionary<string, string> resources,
        IReadOnlyList<string>? criticalKeys = null)
    {
        descriptors.Add(new LanguagePackageDescriptor(
            packageId,
            CultureInfo.GetCultureInfo(cultureName),
            scope)
        {
            ProviderKind = LanguagePackageProviderKind.InMemory,
            ScopeId = scopeId,
            FallbackCulture = fallbackCultureName is null
                ? null
                : CultureInfo.GetCultureInfo(fallbackCultureName),
            ContributionId = contributionId,
            InMemoryResources = resources,
            CriticalResourceKeys = criticalKeys ?? [],
        });
    }

    private static IReadOnlyDictionary<string, string> ToResources(
        IEnumerable<TextEntry> texts,
        bool english)
    {
        return texts.ToDictionary(
            text => text.Key,
            text => english ? text.English : text.Chinese,
            StringComparer.Ordinal);
    }

    private sealed record TextEntry(string Key, string English, string Chinese);
}
