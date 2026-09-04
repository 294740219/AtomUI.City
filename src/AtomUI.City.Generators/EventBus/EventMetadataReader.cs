using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.EventBus;

internal static class EventMetadataReader
{
    private const string ContractAttribute = "AtomUI.City.EventBus.EventContractAttribute";
    private const string ChannelAttribute = "AtomUI.City.EventBus.EventChannelAttribute";
    private const string HandlerAttribute = "AtomUI.City.EventBus.EventHandlerAttribute";
    private const string HandlerInterface = "AtomUI.City.EventBus.IEventHandler<TEvent>";
    private const string ModuleInterface = "AtomUI.City.Core.Modularity.IModule";

    public static EventGenerationCandidate? Read(INamedTypeSymbol symbol)
    {
        var attributes = symbol.GetAttributes();
        var contractAttribute = attributes.FirstOrDefault(attribute => Is(attribute, ContractAttribute));
        var handlerAttribute = attributes.FirstOrDefault(attribute => Is(attribute, HandlerAttribute));
        if (contractAttribute is null && handlerAttribute is null)
        {
            return null;
        }

        var issues = new List<string>();
        GeneratedEventContractMetadata? contract = contractAttribute is null
            ? null
            : ReadContract(symbol, contractAttribute, attributes, issues);
        GeneratedEventHandlerMetadata? handler = handlerAttribute is null
            ? null
            : ReadHandler(symbol, handlerAttribute, issues);

        return new EventGenerationCandidate(contract, handler, symbol.Locations.FirstOrDefault(), issues);
    }

    private static GeneratedEventContractMetadata? ReadContract(
        INamedTypeSymbol symbol,
        AttributeData attribute,
        IReadOnlyList<AttributeData> attributes,
        List<string> issues)
    {
        if (!IsUsableType(symbol))
        {
            issues.Add($"Event contract '{symbol.Name}' must be an accessible, non-generic class or struct.");
        }

        var contractId = ReadString(attribute, 0);
        var owner = ReadType(attribute, 1);
        ValidateName(contractId, "contract id", symbol.Name, issues);
        ValidateOwner(symbol, owner, issues);
        var schemaVersion = ReadInt(attribute, "SchemaVersion", 1);
        if (schemaVersion <= 0)
        {
            issues.Add($"Event contract '{symbol.Name}' must declare a positive SchemaVersion.");
        }

        ValidateObjectGraph(symbol, issues);

        var channels = new List<GeneratedEventChannelMetadata>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var channel in attributes.Where(value => Is(value, ChannelAttribute)))
        {
            var name = ReadString(channel, 0);
            ValidateName(name, "channel name", symbol.Name, issues);
            var capacity = ReadInt(channel, "Capacity", 256);
            var backpressure = ReadInt(channel, "BackpressurePolicy", 0);
            var execution = ReadInt(channel, "ExecutionMode", 0);
            var concurrency = ReadInt(channel, "MaximumConcurrency", 1);
            var timeout = ReadInt(channel, "QueueWaitTimeoutMilliseconds", 0);
            if (name is not null && !names.Add(name)) issues.Add($"Event contract '{symbol.Name}' declares channel '{name}' more than once.");
            if (capacity <= 0) issues.Add($"Event channel '{name}' must have a positive Capacity.");
            if (backpressure is < 0 or > 4) issues.Add($"Event channel '{name}' declares an unknown backpressure policy.");
            if (execution is < 0 or > 2) issues.Add($"Event channel '{name}' declares an unknown execution mode.");
            if (concurrency <= 0 || (execution == 0 && concurrency != 1)) issues.Add($"Event channel '{name}' has an invalid MaximumConcurrency.");
            if (timeout < 0) issues.Add($"Event channel '{name}' cannot have a negative queue timeout.");
            if (name is not null)
            {
                channels.Add(new GeneratedEventChannelMetadata(name, capacity, backpressure, execution, concurrency, timeout));
            }
        }

        return contractId is null || owner is null
            ? null
            : new GeneratedEventContractMetadata(
                symbol, Name(owner), Name(symbol), contractId, schemaVersion, CreateSchemaFingerprint(symbol), channels);
    }

    private static GeneratedEventHandlerMetadata? ReadHandler(
        INamedTypeSymbol symbol,
        AttributeData attribute,
        List<string> issues)
    {
        if (!IsUsableType(symbol) || symbol.TypeKind != TypeKind.Class || symbol.IsAbstract || symbol.IsStatic)
        {
            issues.Add($"Event handler '{symbol.Name}' must be an accessible, concrete, non-generic class.");
        }

        var owner = ReadType(attribute, 0);
        ValidateOwner(symbol, owner, issues);
        var handlerInterfaces = symbol.AllInterfaces.Where(value =>
            value.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == HandlerInterface).ToArray();
        if (handlerInterfaces.Length != 1)
        {
            issues.Add($"Event handler '{symbol.Name}' must implement exactly one IEventHandler<TEvent> contract.");
        }

        var constructors = symbol.InstanceConstructors.Where(value =>
            !value.IsStatic && value.DeclaredAccessibility == Accessibility.Public).ToArray();
        if (constructors.Length != 1 || constructors[0].Parameters.Any(parameter => parameter.RefKind != RefKind.None))
        {
            issues.Add($"Event handler '{symbol.Name}' must expose exactly one public constructor without ref or out parameters.");
        }

        var channelName = ReadNamedString(attribute, "ChannelName") ?? "default";
        ValidateName(channelName, "channel name", symbol.Name, issues);
        var dispatchPolicy = ReadInt(attribute, "DispatchPolicy", 3);
        var dispatchMode = ReadInt(attribute, "DispatchMode", 1);
        var errorPolicy = ReadInt(attribute, "ErrorPolicy", 0);
        var timeout = ReadInt(attribute, "HandlerTimeoutMilliseconds", 30000);
        var disableAfter = ReadInt(attribute, "DisableSubscriptionAfterFailures", 3);
        if (dispatchPolicy is < 0 or > 3) issues.Add($"Event handler '{symbol.Name}' declares an unknown dispatch policy.");
        if (dispatchMode is < 0 or > 1) issues.Add($"Event handler '{symbol.Name}' declares an unknown dispatch mode.");
        if (errorPolicy is < 0 or > 3) issues.Add($"Event handler '{symbol.Name}' declares an unknown error policy.");
        if (timeout < 0) issues.Add($"Event handler '{symbol.Name}' cannot have a negative handler timeout.");
        if (disableAfter <= 0) issues.Add($"Event handler '{symbol.Name}' must have a positive failure threshold.");

        if (owner is null || handlerInterfaces.Length != 1 || constructors.Length != 1)
        {
            return null;
        }

        var eventType = handlerInterfaces[0].TypeArguments[0];
        return new GeneratedEventHandlerMetadata(
            eventType, Name(owner), Name(eventType), Name(symbol), channelName,
            dispatchPolicy, dispatchMode, errorPolicy, timeout, disableAfter,
            constructors[0].Parameters.Select(parameter => Name(parameter.Type)).ToArray());
    }

    private static void ValidateOwner(INamedTypeSymbol declaration, INamedTypeSymbol? owner, List<string> issues)
    {
        if (owner is null || !owner.AllInterfaces.Any(value => Name(value) == ModuleInterface))
        {
            issues.Add($"Generated event declaration '{declaration.Name}' must name an IModule owner.");
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(declaration.ContainingAssembly, owner.ContainingAssembly))
        {
            issues.Add($"Generated event declaration '{declaration.Name}' cannot claim a Module owner from another assembly.");
        }
    }

    private static void ValidateObjectGraph(INamedTypeSymbol root, List<string> issues)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        Visit(root, root, visited, issues);
    }

    private static string CreateSchemaFingerprint(INamedTypeSymbol root)
    {
        var parts = new List<string>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        AppendSchema(root, root, visited, parts);
        var canonical = string.Join("|", parts.OrderBy(value => value, StringComparer.Ordinal));
        var hash = 14695981039346656037UL;
        foreach (var character in canonical)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }
        return hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AppendSchema(INamedTypeSymbol root, ITypeSymbol type,
        HashSet<ITypeSymbol> visited, List<string> parts)
    {
        if (!visited.Add(type)) return;
        parts.Add(Name(type));
        if (type is IArrayTypeSymbol array)
        {
            AppendSchema(root, array.ElementType, visited, parts);
            return;
        }
        if (type is not INamedTypeSymbol named) return;
        foreach (var argument in named.TypeArguments) AppendSchema(root, argument, visited, parts);
        if (!SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, root.ContainingAssembly)) return;
        foreach (var member in named.GetMembers().Where(value => !value.IsStatic).OrderBy(value => value.Name, StringComparer.Ordinal))
        {
            var memberType = member switch
            {
                IPropertySymbol property when property.DeclaredAccessibility == Accessibility.Public => property.Type,
                IFieldSymbol field when field.DeclaredAccessibility == Accessibility.Public => field.Type,
                _ => null,
            };
            if (memberType is null) continue;
            parts.Add($"{Name(named)}.{member.Name}:{Name(memberType)}");
            AppendSchema(root, memberType, visited, parts);
        }
    }

    private static void Visit(INamedTypeSymbol root, ITypeSymbol type, HashSet<ITypeSymbol> visited, List<string> issues)
    {
        if (!visited.Add(type)) return;

        if (type is IDynamicTypeSymbol || type is ITypeParameterSymbol ||
            type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
        {
            Reject(root, type, issues);
            return;
        }

        if (type is IArrayTypeSymbol)
        {
            Reject(root, type, issues);
            return;
        }

        if (type is not INamedTypeSymbol named)
        {
            Reject(root, type, issues);
            return;
        }

        if (IsAllowedScalar(named))
        {
            return;
        }

        if (named.TypeKind == TypeKind.Enum)
        {
            if (!SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, root.ContainingAssembly))
            {
                Reject(root, named, issues);
            }
            return;
        }

        var fullName = Name(named.OriginalDefinition);
        if (fullName is "System.Nullable<T>" or
            "System.Collections.Immutable.ImmutableArray<T>" or
            "System.Collections.Generic.KeyValuePair<TKey, TValue>")
        {
            foreach (var argument in named.TypeArguments)
            {
                Visit(root, argument, visited, issues);
            }
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, root.ContainingAssembly) ||
            named.Arity != 0 || named.IsUnboundGenericType || named.IsRefLikeType ||
            named.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            Reject(root, named, issues);
            return;
        }

        if (named.TypeKind == TypeKind.Class &&
            (!named.IsSealed || named.IsAbstract || named.BaseType?.SpecialType != SpecialType.System_Object))
        {
            Reject(root, named, issues, "must be a sealed, non-abstract contract-local class with no custom base class");
            return;
        }

        if (named.TypeKind == TypeKind.Struct && !named.IsReadOnly)
        {
            Reject(root, named, issues, "must be a readonly contract-local struct");
            return;
        }

        foreach (var field in named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic))
        {
            var immutableAutoPropertyField = field.AssociatedSymbol is IPropertySymbol property &&
                                             (property.SetMethod is null || property.SetMethod.IsInitOnly);
            if (!field.IsReadOnly && !immutableAutoPropertyField)
            {
                Reject(root, field.Type, issues,
                    $"is stored by mutable field '{Name(named)}.{field.Name}'");
            }
            Visit(root, field.Type, visited, issues);
        }

        foreach (var property in named.GetMembers().OfType<IPropertySymbol>().Where(property =>
                     !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public))
        {
            if (property.IsIndexer || property.GetMethod is null ||
                property.SetMethod is not null && !property.SetMethod.IsInitOnly)
            {
                Reject(root, property.Type, issues,
                    $"is exposed by mutable or indexed property '{Name(named)}.{property.Name}'");
            }
            Visit(root, property.Type, visited, issues);
        }
    }

    private static bool IsAllowedScalar(INamedTypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Boolean or
            SpecialType.System_Char or
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Decimal or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_String or
            SpecialType.System_DateTime)
        {
            return true;
        }

        return Name(type) is "System.Guid" or
            "System.DateTimeOffset" or
            "System.DateOnly" or
            "System.TimeOnly" or
            "System.TimeSpan";
    }

    private static void Reject(
        INamedTypeSymbol root,
        ITypeSymbol type,
        List<string> issues,
        string? reason = null)
    {
        var suffix = reason is null ? string.Empty : $" and {reason}";
        issues.Add(
            $"Shared event contract '{root.Name}' contains object-graph type '{Name(type)}' that is not in the closed allowlist{suffix}.");
    }

    private static bool IsUsableType(INamedTypeSymbol symbol) =>
        symbol.Arity == 0 && symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal &&
        symbol.TypeKind is TypeKind.Class or TypeKind.Struct;

    private static bool Is(AttributeData attribute, string name) => Name(attribute.AttributeClass) == name;
    private static string Name(ITypeSymbol? symbol) => symbol?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;
    private static string? ReadString(AttributeData attribute, int index) => attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as string : null;
    private static INamedTypeSymbol? ReadType(AttributeData attribute, int index) => attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as INamedTypeSymbol : null;
    private static string? ReadNamedString(AttributeData attribute, string name) => attribute.NamedArguments.FirstOrDefault(value => value.Key == name).Value.Value as string;
    private static int ReadInt(AttributeData attribute, string name, int fallback)
    {
        var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name);
        return value.Key is null || value.Value.Value is not int result ? fallback : result;
    }

    private static void ValidateName(string? value, string kind, string target, List<string> issues)
    {
        if (value is null || string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Any(char.IsControl))
            issues.Add($"Generated event declaration '{target}' has an invalid {kind}.");
    }
}

internal sealed class EventGenerationCandidate
{
    public EventGenerationCandidate(GeneratedEventContractMetadata? contract, GeneratedEventHandlerMetadata? handler,
        Location? location, IReadOnlyList<string> issues)
    {
        Contract = contract;
        Handler = handler;
        Location = location;
        Issues = issues;
    }

    public GeneratedEventContractMetadata? Contract { get; }
    public GeneratedEventHandlerMetadata? Handler { get; }
    public Location? Location { get; }
    public IReadOnlyList<string> Issues { get; }
}
