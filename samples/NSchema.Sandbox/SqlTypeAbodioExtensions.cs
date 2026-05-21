using NSchema.Schema;

namespace NSchema.Sandbox;

internal static class SqlTypeAbodioExtensions
{
    extension(SqlType)
    {
        public static SqlType TypeId => SqlType.Custom("typeid");
    }
}
