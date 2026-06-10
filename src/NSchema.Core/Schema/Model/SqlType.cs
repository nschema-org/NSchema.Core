namespace NSchema.Schema.Model;

/// <summary>
/// Represents a SQL data type used for defining columns in a database schema.
/// </summary>
/// <param name="Name">The canonical type name, e.g. <c>"varchar"</c>, <c>"decimal"</c>, <c>"citext"</c>.</param>
public sealed record SqlType(string Name)
{
    /// <summary>
    /// The canonical type name, e.g. <c>"varchar"</c>, <c>"decimal"</c>, <c>"citext"</c>.
    /// </summary>
    public string Name { get; init; } = Name.Trim().ToLowerInvariant();

    /// <summary>
    /// The length facet for fixed- and variable-length types (char/varchar/binary). Null means unbounded.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// The precision facet for <c>decimal</c> (total number of digits).
    /// </summary>
    public int? Precision { get; init; }

    /// <summary>
    /// The scale facet for <c>decimal</c> (digits to the right of the decimal point).
    /// </summary>
    public int? Scale { get; init; }

    /// <summary>
    /// The SQL boolean type, representing true/false values.
    /// </summary>
    public static readonly SqlType Boolean = new("boolean");

    /// <summary>
    /// The SQL tinyint type, representing small integer values (0 to 255).
    /// </summary>
    public static readonly SqlType TinyInt = new("tinyint");

    /// <summary>
    /// The SQL smallint type, representing small integer values (-32,768 to 32,767).
    /// </summary>
    public static readonly SqlType SmallInt = new("smallint");

    /// <summary>
    /// The SQL int type, representing integer values (-2,147,483,648 to 2,147,483,647).
    /// </summary>
    public static readonly SqlType Int = new("int");

    /// <summary>
    /// The SQL bigint type, representing large integer values.
    /// </summary>
    public static readonly SqlType BigInt = new("bigint");

    /// <summary>
    /// The SQL float type, representing approximate single-precision numeric values.
    /// </summary>
    public static readonly SqlType Float = new("float");

    /// <summary>
    /// The SQL double type, representing approximate double-precision numeric values.
    /// </summary>
    public static readonly SqlType Double = new("double");

    /// <summary>
    /// The SQL text type, representing large character data.
    /// </summary>
    public static readonly SqlType Text = new("text");

    /// <summary>
    /// The SQL date type, representing calendar dates (year, month, day).
    /// </summary>
    public static readonly SqlType Date = new("date");

    /// <summary>
    /// The SQL time type, representing time of day (hour, minute, second).
    /// </summary>
    public static readonly SqlType Time = new("time");

    /// <summary>
    /// The SQL datetime type, representing date and time values without time zone.
    /// </summary>
    public static readonly SqlType DateTime = new("datetime");

    /// <summary>
    /// The SQL datetimeoffset type, representing date and time values with time zone awareness.
    /// </summary>
    public static readonly SqlType DateTimeOffset = new("datetimeoffset");

    /// <summary>
    /// The SQL guid type, representing globally unique identifiers (GUIDs/UUIDs).
    /// </summary>
    public static readonly SqlType Guid = new("guid");

    /// <summary>
    /// The SQL decimal type, representing fixed-point numeric values with specified precision and scale.
    /// </summary>
    /// <param name="precision">The total number of digits that can be stored.</param>
    /// <param name="scale">The number of digits that can be stored to the right of the decimal point.</param>
    public static SqlType Decimal(int precision, int scale) => new("decimal") { Precision = precision, Scale = scale };

    /// <summary>
    /// The SQL char type, representing fixed-length character data.
    /// </summary>
    /// <param name="length">The number of characters.</param>
    public static SqlType Char(int length) => new("char") { Length = length };

    /// <summary>
    /// The SQL nchar type, representing fixed-length Unicode character data.
    /// </summary>
    /// <param name="length">The number of characters.</param>
    public static SqlType NChar(int length) => new("nchar") { Length = length };

    /// <summary>
    /// The SQL varchar type, representing variable-length character data.
    /// </summary>
    /// <param name="maxLength">The maximum number of characters, or null for unbounded.</param>
    public static SqlType VarChar(int? maxLength = null) => new("varchar") { Length = maxLength };

    /// <summary>
    /// The SQL nvarchar type, representing variable-length Unicode character data.
    /// </summary>
    /// <param name="maxLength">The maximum number of characters, or null for unbounded.</param>
    public static SqlType NVarChar(int? maxLength = null) => new("nvarchar") { Length = maxLength };

    /// <summary>
    /// The SQL binary type, representing fixed-length binary data.
    /// </summary>
    /// <param name="length">The number of bytes.</param>
    public static SqlType Binary(int length) => new("binary") { Length = length };

    /// <summary>
    /// The SQL varbinary type, representing variable-length binary data.
    /// </summary>
    /// <param name="maxLength">The maximum number of bytes, or null for unbounded.</param>
    public static SqlType VarBinary(int? maxLength = null) => new("varbinary") { Length = maxLength };

    /// <summary>
    /// A SQL type identified by a raw name the library has no special knowledge of, such as a
    /// database-specific or user-defined type (e.g. Postgres' <c>citext</c>).
    /// </summary>
    /// <param name="typeName">The type name recognized by the target database system.</param>
    public static SqlType Custom(string typeName) => new(typeName);

    /// <summary>
    /// Renders the canonical string form, e.g. <c>"bigint"</c>, <c>"varchar(255)"</c>, <c>"decimal(18,2)"</c>.
    /// </summary>
    public override string ToString()
    {
        if (Precision is { } precision)
        {
            return $"{Name}({precision},{Scale})";
        }

        if (Length is { } length)
        {
            return $"{Name}({length})";
        }

        return Name;
    }

    /// <summary>
    /// Parses a SQL type from its canonical string representation, as produced by <see cref="ToString"/>.
    /// An unrecognized name is preserved verbatim as a <see cref="Custom"/> type.
    /// </summary>
    public static SqlType Parse(string value)
    {
        var (name, args) = Tokenize(value);
        return name switch
        {
            "boolean" => Boolean,
            "tinyint" => TinyInt,
            "smallint" => SmallInt,
            "int" or "integer" => Int,
            "bigint" => BigInt,
            "float" => Float,
            "double" => Double,
            "text" => Text,
            "date" => Date,
            "time" => Time,
            "datetime" => DateTime,
            "datetimeoffset" => DateTimeOffset,
            "guid" => Guid,
            "decimal" => Decimal(Arg(0), Arg(1)),
            "char" => Char(Arg(0)),
            "nchar" => NChar(Arg(0)),
            "binary" => Binary(Arg(0)),
            "varchar" => VarChar(OptionalArg(0)),
            "nvarchar" => NVarChar(OptionalArg(0)),
            "varbinary" => VarBinary(OptionalArg(0)),
            _ => Custom(value),
        };

        int Arg(int index) => OptionalArg(index) ?? throw new FormatException($"SqlType \"{name}\" is missing required argument {index} in \"{value}\".");
        int? OptionalArg(int index) => index < args.Count ? int.Parse(args[index]) : null;
    }

    /// <summary>
    /// Splits a type string into its lower-cased base name and any comma-separated arguments,
    /// e.g. <c>"decimal(10, 2)"</c> becomes <c>("decimal", ["10", "2"])</c>.
    /// </summary>
    private static (string Name, IReadOnlyList<string> Args) Tokenize(string value)
    {
        var parenStart = value.IndexOf('(');
        if (parenStart < 0)
        {
            return (value.ToLowerInvariant(), []);
        }

        var parenEnd = value.LastIndexOf(')');
        if (parenEnd < parenStart)
        {
            throw new FormatException($"Malformed SqlType string: \"{value}\".");
        }

        var name = value[..parenStart].ToLowerInvariant();
        var args = value[(parenStart + 1)..parenEnd].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return (name, args);
    }
}
