using NSchema.Schema;

namespace NSchema.Postgres;

public static class SqlTypePostgresExtensions
{
    extension(SqlType)
    {
        public static SqlType Citext => SqlType.Custom("citext");
        public static SqlType Jsonb  => SqlType.Custom("jsonb");
    }
}
