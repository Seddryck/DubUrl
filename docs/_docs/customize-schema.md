---
title: Customize validation and conversion
parent: Working with schemas
nav_order: 7

---

This guide is for applications embedding `DubUrl.Schema.Documents`. Start with [validation]({{ '/docs/validate-schema/' | relative_url }}) and use the defaults unless your application needs different rules.

## Configurable validation

`SchemaDocumentValidator` uses ordered default sets for columns, foreign keys, indexes, checks, view declarations, resolved views, and the complete view graph. Create a rule set when an application needs to suppress one of those defaults or append an application-specific rule:

```csharp
using System.Data;
using System.Globalization;
using DubUrl.Schema.Documents;

var rules = SchemaDocumentValidationRuleSet.CreateDefault()
    .WithColumnRule(new ApplicationColumnRule());

var validator = new SchemaDocumentValidator(rules);
```

The application rule below reports a warning when a column description is missing. Declare this type separately from the setup statements:

```csharp
using DubUrl.Schema.Documents;

public sealed class ApplicationColumnRule : IValidationRule<ColumnValidationContext>
{
    public string Id => "application.column-description";

    public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(context.Column.Description))
            diagnostics.Add(new SchemaDocumentDiagnostic(
                "APP001", SchemaDocumentDiagnosticSeverity.Warning,
                "Add a column description.", context.Path));
    }
}
```

Custom rules implement `IValidationRule<TContext>`. View declaration rules receive duplicate and table-collision resolution, view rules receive the resolved source table or view and its output columns, and graph rules receive the complete valid view lookup. This separation keeps forward references valid while allowing dependency-cycle validation to be configured independently.

Rule sets are immutable, validators snapshot their selected rules, and rule execution follows registration order so diagnostics remain deterministic. The parameterless validator always uses the complete default set. Removing referential or graph rules can admit a document that the runtime schema model cannot construct, so applications should remove defaults only when they replace the corresponding guarantees.

## Configurable literal conversion

Literal converters are provider-independent: they turn a version 1 logical literal into its canonical CLR value. Database-specific formatting and adaptation remain the renderer's responsibility.

Start from the complete immutable registry and explicitly replace, add, or remove converters. A converter owns one `DbType`, has a stable identifier, and returns a non-throwing `LiteralConversionResult`:

```csharp
public sealed class HexInt32Converter : ILiteralValueConverter
{
    public string Id => "application.hex-int32";
    public DbType Type => DbType.Int32;

    public LiteralConversionResult TryConvert(string value)
        => int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result)
            ? LiteralConversionResult.Success(result)
            : LiteralConversionResult.Failure();
}
```

Apply the converter before reading its validation and mapping results:

```csharp
var converters = SchemaDocumentLiteralConverterRegistry.CreateDefault()
    .ReplaceConverter(new HexInt32Converter());

var validator = new SchemaDocumentValidator(converters);
var mapper = new SchemaDocumentMapper(validator);
```

Passing the configured validator to the mapper guarantees that validation and mapping use the same converter snapshot. `SchemaDocumentMapper` also accepts a converter registry directly. Duplicate converter identifiers and ambiguous ownership of a `DbType` are rejected; use `ReplaceConverter` when overriding an existing conversion.

Changing a converter changes how an existing version 1 logical type becomes a CLR value; it does not add a new logical type to the published version 1 vocabulary.
