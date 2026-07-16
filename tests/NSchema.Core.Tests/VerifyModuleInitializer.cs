using System.Runtime.CompilerServices;
using NSchema.Model.Columns;
using NSchema.Model.Routines;

namespace NSchema.Tests;

/// <summary>
/// Global Verify configuration. Snapshots live in a <c>Snapshots</c> folder next to the
/// test source file that produced them, matching the existing convention under <c>State/Snapshots</c>.
/// </summary>
public static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        DerivePathInfo((sourceFile, _, type, method) => new PathInfo(
            directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name
        ));

        // SqlType is a discriminated union (IntType, BigIntType, ...) whose identity lives in the
        // runtime type, not a property — so Verify's default serializer renders every value as "{}".
        // Its ToString() is the canonical form ("bigint", "varchar(100)"); render it as that scalar.
        // A converter (rather than TreatAsString<SqlType>) is needed so it matches the subclasses too.
        VerifierSettings.AddExtraSettings(settings => settings.Converters.Add(new SqlTypeConverter()));

        // A value object (identifier, opaque SQL) is a string in every rendered form; snapshots show
        // its exact underlying text.
        VerifierSettings.AddExtraSettings(settings => settings.Converters.Add(new ValueObjectConverter()));

        // Addresses and routine references render as written.
        VerifierSettings.AddExtraSettings(settings => settings.Converters.Add(new ObjectReferenceConverter()));
        VerifierSettings.AddExtraSettings(settings => settings.Converters.Add(new RoutineReferenceConverter()));
    }

    private sealed class SqlTypeConverter : WriteOnlyJsonConverter
    {
        public override bool CanConvert(Type type) => typeof(SqlType).IsAssignableFrom(type);

        public override void Write(VerifyJsonWriter writer, object value) => writer.WriteValue(value.ToString());
    }

    private sealed class ValueObjectConverter : WriteOnlyJsonConverter
    {
        public override bool CanConvert(Type type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Model.ValueObject<>))
                {
                    return true;
                }
            }

            return false;
        }

        // ToString is sealed on the base as the value's own rendering, so this is the exact underlying text.
        public override void Write(VerifyJsonWriter writer, object value) => writer.WriteValue(value.ToString());
    }

    private sealed class ObjectReferenceConverter : WriteOnlyJsonConverter<Model.ObjectReference>
    {
        public override void Write(VerifyJsonWriter writer, Model.ObjectReference value) =>
            writer.WriteValue(value.ToString());
    }

    private sealed class RoutineReferenceConverter : WriteOnlyJsonConverter<RoutineReference>
    {
        public override void Write(VerifyJsonWriter writer, RoutineReference value) =>
            writer.WriteValue(value.ToString());
    }
}
