using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Antlr.Runtime.Tree;
using Antlr4.StringTemplate;
using Antlr4.StringTemplate.Compiler;
using Antlr4.StringTemplate.Misc;

namespace DubUrl.Querying.Templating;

/// <summary>A reusable SQL definition with an exact, case-sensitive public-property input contract.</summary>
public abstract class TypedCommandTemplate<TParameters>
{
    public abstract string TemplateText { get; }

    // Cache only definitions and accessors, never values, bindings, or mutable renderers.
    private static readonly PropertyContract[] Properties = typeof(TParameters)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.GetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0)
        .Select(p => new PropertyContract(p)).ToArray();
    private static readonly ConcurrentDictionary<string, Lazy<bool>> Validated = new(StringComparer.Ordinal);

    public string Render(IDatabaseUrl database, TParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(database);
        var (text, values) = Prepare(parameters);
        return new InlineTemplateCommandFactory(database.Dialect).Create(text).Render(values);
    }

    public RenderedTemplateCommand RenderCommand(DatabaseUrl database, TParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(database);
        var (text, values) = Prepare(parameters);
        return database.CreateTemplate(text).RenderCommand(values);
    }

    /// <summary>Snapshots inputs for execution through any existing query API.</summary>
    public DubUrl.Querying.ICommandProvider Bind(TParameters parameters)
    {
        var (text, values) = Prepare(parameters);
        return new TypedTemplateCommand(text, values);
    }

    private (string, IDictionary<string, object?>) Prepare(TParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var source = TemplateText;
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        _ = Validated.GetOrAdd(source, text => new Lazy<bool>(() => Validate(text))).Value;
        return (source, Properties.ToDictionary(p => p.Name, p => p.Read(parameters), StringComparer.Ordinal));
    }

    private static bool Validate(string source)
    {
        // Inspect the existing engine's syntax tree, including inactive branches and loop scopes.
        // No second template parser or renderer is introduced.
        var group = new TemplateGroup('$', '$') { Listener = new ContractErrorListener() };
        var compiled = new Template(group, source).impl;
        if (compiled.Ast is null) throw new ArgumentException("Invalid SQL template.", nameof(source));
        Visit(compiled.Ast, new HashSet<string>(StringComparer.Ordinal));
        return true;
    }

    private static void Visit(CommonTree node, HashSet<string> locals)
    {
        if (node.Type == TemplateParser.SUBTEMPLATE)
        {
            locals = new HashSet<string>(locals, StringComparer.Ordinal);
            locals.UnionWith(["i", "i0"]);
            foreach (CommonTree args in node.Children ?? [])
                if (args.Type == TemplateParser.ARGS)
                    foreach (CommonTree arg in args.Children ?? []) locals.Add(arg.Text);
        }
        if (node.Type == TemplateParser.ARGS) return;
        if (node.Type == TemplateParser.OPTIONS)
        {
            foreach (CommonTree option in node.Children ?? [])
                for (var index = 1; index < option.ChildCount; index++) Visit((CommonTree)option.GetChild(index), locals);
            return;
        }
        if (node.Type == TemplateParser.EXEC_FUNC)
        {
            // Built-in function names are syntax, while their arguments are inputs.
            for (var index = 1; index < node.ChildCount; index++) Visit((CommonTree)node.GetChild(index), locals);
            return;
        }
        if (node.Type == TemplateParser.PROP)
        {
            var target = (CommonTree)node.GetChild(0);
            if (target.Type == TemplateParser.ID && !locals.Contains(target.Text)
                && Properties.FirstOrDefault(p => p.Name == target.Text) is { IsOptional: true })
            {
                var member = node.GetChild(1).Text;
                if (member is not ("HasValue" or "Value"))
                    throw new ArgumentException($"Unknown optional member '{target.Text}.{member}'. Use HasValue or Value (case-sensitive).");
                return;
            }
            Visit(target, locals);
            return;
        }
        if (node.Type == TemplateParser.ID && !locals.Contains(node.Text))
        {
            var property = Properties.FirstOrDefault(p => p.Name == node.Text)
                ?? throw new ArgumentException($"Unknown template input '{node.Text}' on {typeof(TParameters).Name}. Property names are case-sensitive.");
            if (property.IsOptional)
                throw new ArgumentException($"Optional input '{node.Text}' requires an explicit .HasValue presence check or .Value access.");
        }
        foreach (CommonTree child in node.Children ?? []) Visit(child, locals);
    }

    private sealed class PropertyContract
    {
        public string Name { get; }
        public bool IsOptional { get; }
        private readonly Func<TParameters, object?> _get;
        private readonly Func<object, bool>? _hasValue;
        private readonly Func<object, object?>? _optionalValue;
        private readonly bool _allowsNull;

        public PropertyContract(PropertyInfo property)
        {
            Name = property.Name;
            var input = Expression.Parameter(typeof(TParameters));
            _get = Expression.Lambda<Func<TParameters, object?>>(Expression.Convert(Expression.Property(input, property), typeof(object)), input).Compile();
            var type = property.PropertyType;
            var nullability = new NullabilityInfoContext().Create(property);
            IsOptional = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);
            if (IsOptional)
            {
                var boxed = Expression.Parameter(typeof(object));
                var optional = Expression.Convert(boxed, type);
                _hasValue = Expression.Lambda<Func<object, bool>>(Expression.Property(optional, "HasValue"), boxed).Compile();
                _optionalValue = Expression.Lambda<Func<object, object?>>(Expression.Convert(Expression.Property(optional, "Value"), typeof(object)), boxed).Compile();
                type = type.GetGenericArguments()[0];
                nullability = nullability.GenericTypeArguments[0];
            }
            _allowsNull = Nullable.GetUnderlyingType(type) is not null || (!type.IsValueType && nullability.ReadState == NullabilityState.Nullable);
        }

        public object? Read(TParameters parameters)
        {
            var value = _get(parameters);
            if (!IsOptional) return ValidateNull(value);
            var present = _hasValue!(value!);
            return new Dictionary<string, object?>
            {
                ["HasValue"] = present,
                ["Value"] = present ? ValidateNull(_optionalValue!(value!)) : new AbsentTemplateInput(Name)
            };
        }

        private object? ValidateNull(object? value)
        {
            if (value is null && !_allowsNull)
                throw new ArgumentException($"Input '{Name}' does not permit null. Use a nullable type to explicitly allow null.");
            return value;
        }
    }

    private sealed class ContractErrorListener : ITemplateErrorListener
    {
        private static void Fail(TemplateMessage message) => throw new ArgumentException($"Invalid SQL template: {message}");
        public void CompiletimeError(TemplateMessage message) => Fail(message);
        public void RuntimeError(TemplateMessage message) => Fail(message);
        public void IOError(TemplateMessage message) => Fail(message);
        public void InternalError(TemplateMessage message) => Fail(message);
    }
}
