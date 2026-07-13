using Microsoft.Extensions.Logging;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Diff.Domain;

internal sealed partial class SchemaComparer
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Beginning schema comparison")]
    private partial void LogBeginningComparison();

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Comparison complete: {ActionCount} actions generated")]
    private partial void LogComparisonComplete(int actionCount);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Schema '{Schema}' exists in desired state")]
    private partial void LogSchemaExists(SqlIdentifier schema);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Schema '{Schema}' not found in desired state")]
    private partial void LogSchemaNotInDesired(SqlIdentifier schema);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Schema '{Schema}' is new")]
    private partial void LogSchemaNew(SqlIdentifier schema);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "Schema '{Schema}' is unchanged")]
    private partial void LogSchemaUnchanged(SqlIdentifier schema);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Schema '{OldName}' renamed to '{NewName}'")]
    private partial void LogSchemaRenamed(SqlIdentifier oldName, SqlIdentifier newName);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Schema '{Schema}' comment changed")]
    private partial void LogSchemaCommentChanged(SqlIdentifier schema);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Table '{Schema}.{Table}' exists in desired state")]
    private partial void LogTableExists(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' explicitly marked for removal")]
    private partial void LogTableExplicitlyDropped(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 22, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' not found in desired state")]
    private partial void LogTableNotInDesired(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 23, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' not in desired state; skipping (partial schema)")]
    private partial void LogTableSkippedPartial(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 24, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' is new")]
    private partial void LogTableNew(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 25, Level = LogLevel.Debug, Message = "Table '{Schema}.{Table}' is unchanged")]
    private partial void LogTableUnchanged(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 26, Level = LogLevel.Information, Message = "Table '{Schema}.{OldName}' renamed to '{NewName}'")]
    private partial void LogTableRenamed(SqlIdentifier schema, SqlIdentifier oldName, SqlIdentifier newName);

    [LoggerMessage(EventId = 27, Level = LogLevel.Information, Message = "Table '{Schema}.{Table}' comment changed")]
    private partial void LogTableCommentChanged(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 28, Level = LogLevel.Information, Message = "Creating table '{Schema}.{Table}'")]
    private partial void LogTableCreating(SqlIdentifier schema, SqlIdentifier table);

    [LoggerMessage(EventId = 30, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' exists in desired state")]
    private partial void LogColumnExists(ObjectReference owner, SqlIdentifier column);

    [LoggerMessage(EventId = 31, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' not found in desired state")]
    private partial void LogColumnNotInDesired(ObjectReference owner, SqlIdentifier column);

    [LoggerMessage(EventId = 32, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' is new")]
    private partial void LogColumnNew(ObjectReference owner, SqlIdentifier column);

    [LoggerMessage(EventId = 33, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' is unchanged")]
    private partial void LogColumnUnchanged(ObjectReference owner, SqlIdentifier column);

    [LoggerMessage(EventId = 34, Level = LogLevel.Information, Message = "Column '{Owner}.{OldName}' renamed to '{NewName}'")]
    private partial void LogColumnRenamed(ObjectReference owner, SqlIdentifier oldName, SqlIdentifier newName);

    [LoggerMessage(EventId = 35, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' type is unchanged ({Type})")]
    private partial void LogColumnTypeUnchanged(ObjectReference owner, SqlIdentifier column, SqlType type);

    [LoggerMessage(EventId = 36, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' type changed: {OldType} -> {NewType}")]
    private partial void LogColumnTypeChanged(ObjectReference owner, SqlIdentifier column, SqlType oldType, SqlType newType);

    [LoggerMessage(EventId = 37, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' nullability is unchanged ({Nullability})")]
    private partial void LogColumnNullabilityUnchanged(ObjectReference owner, SqlIdentifier column, string nullability);

    [LoggerMessage(EventId = 38, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' nullability changed: {OldValue} -> {NewValue}")]
    private partial void LogColumnNullabilityChanged(ObjectReference owner, SqlIdentifier column, bool oldValue, bool newValue);

    [LoggerMessage(EventId = 39, Level = LogLevel.Debug, Message = "Column '{Owner}.{Column}' default is unchanged ({Default})")]
    private partial void LogColumnDefaultUnchanged(ObjectReference owner, SqlIdentifier column, string @default);

    [LoggerMessage(EventId = 40, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' default changed: '{OldDefault}' -> '{NewDefault}'")]
    private partial void LogColumnDefaultChanged(ObjectReference owner, SqlIdentifier column, SqlText? oldDefault, SqlText? newDefault);

    [LoggerMessage(EventId = 41, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' comment changed")]
    private partial void LogColumnCommentChanged(ObjectReference owner, SqlIdentifier column);

    [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Column '{Owner}.{Column}' identity sequence options changed: start {OldStart} -> {NewStart}, min {OldMin} -> {NewMin}, increment {OldIncrement} -> {NewIncrement}")]
    private partial void LogColumnIdentityChanged(ObjectReference owner, SqlIdentifier column, long? oldStart, long? newStart, long? oldMin, long? newMin, long? oldIncrement, long? newIncrement);

    [LoggerMessage(EventId = 50, Level = LogLevel.Debug, Message = "Primary key for '{Owner}' is unchanged")]
    private partial void LogPrimaryKeyUnchanged(ObjectReference owner);

    [LoggerMessage(EventId = 51, Level = LogLevel.Information, Message = "Dropping primary key '{KeyName}' from '{Owner}'")]
    private partial void LogPrimaryKeyDropping(SqlIdentifier keyName, ObjectReference owner);

    [LoggerMessage(EventId = 52, Level = LogLevel.Information, Message = "Adding primary key '{KeyName}' to '{Owner}'")]
    private partial void LogPrimaryKeyAdding(SqlIdentifier keyName, ObjectReference owner);

    [LoggerMessage(EventId = 63, Level = LogLevel.Information, Message = "Adding foreign key '{FkName}' to new table '{Owner}'")]
    private partial void LogForeignKeyAddingToNewTable(SqlIdentifier fkName, ObjectReference owner);

    [LoggerMessage(EventId = 67, Level = LogLevel.Information, Message = "Adding unique constraint '{Name}' to new table '{Owner}'")]
    private partial void LogUniqueConstraintAddingToNewTable(SqlIdentifier name, ObjectReference owner);

    [LoggerMessage(EventId = 68, Level = LogLevel.Information, Message = "Primary key '{Name}' on '{Owner}' comment changed")]
    private partial void LogPrimaryKeyCommentChanged(SqlIdentifier name, ObjectReference owner);

    // Shared by CompareTableMembers for every list-member kind (foreign keys, unique/check constraints, indexes).
    [LoggerMessage(EventId = 60, Level = LogLevel.Information, Message = "{MemberKind} '{Name}' on '{Owner}' is missing or changed")]
    private partial void LogTableMemberMissingOrChanged(string memberKind, SqlIdentifier name, ObjectReference owner);

    [LoggerMessage(EventId = 61, Level = LogLevel.Debug, Message = "{MemberKind} '{Name}' on '{Owner}' is unchanged")]
    private partial void LogTableMemberUnchanged(string memberKind, SqlIdentifier name, ObjectReference owner);

    [LoggerMessage(EventId = 62, Level = LogLevel.Information, Message = "{MemberKind} '{Name}' on '{Owner}' is new or changed")]
    private partial void LogTableMemberNewOrChanged(string memberKind, SqlIdentifier name, ObjectReference owner);

    [LoggerMessage(EventId = 64, Level = LogLevel.Information, Message = "{MemberKind} '{Name}' on '{Owner}' comment changed")]
    private partial void LogTableMemberCommentChanged(string memberKind, SqlIdentifier name, ObjectReference owner);

    [LoggerMessage(EventId = 93, Level = LogLevel.Information, Message = "Adding check constraint '{Name}' to new table '{Owner}'")]
    private partial void LogCheckConstraintAddingToNewTable(SqlIdentifier name, ObjectReference owner);

    [LoggerMessage(EventId = 74, Level = LogLevel.Information, Message = "Adding index '{IndexName}' to new table '{Owner}'")]
    private partial void LogIndexAddingToNewTable(SqlIdentifier indexName, ObjectReference owner);

    [LoggerMessage(EventId = 80, Level = LogLevel.Information, Message = "Revoking USAGE on schema '{Schema}' from '{Role}'")]
    private partial void LogSchemaUsageRevoking(SqlIdentifier schema, SqlIdentifier role);

    [LoggerMessage(EventId = 81, Level = LogLevel.Information, Message = "Granting USAGE on schema '{Schema}' to '{Role}'")]
    private partial void LogSchemaUsageGranting(SqlIdentifier schema, SqlIdentifier role);

    [LoggerMessage(EventId = 82, Level = LogLevel.Information, Message = "Revoking all privileges on '{Owner}' from '{Role}'")]
    private partial void LogTablePrivilegesRevoking(ObjectReference owner, SqlIdentifier role);

    [LoggerMessage(EventId = 83, Level = LogLevel.Information, Message = "Updating privileges on '{Owner}' for '{Role}'")]
    private partial void LogTablePrivilegesUpdating(ObjectReference owner, SqlIdentifier role);

    [LoggerMessage(EventId = 84, Level = LogLevel.Information, Message = "Granting privileges on '{Owner}' to '{Role}'")]
    private partial void LogTablePrivilegesGranting(ObjectReference owner, SqlIdentifier role);

    [LoggerMessage(EventId = 85, Level = LogLevel.Information, Message = "Granting privileges on new table '{Owner}' to '{Role}'")]
    private partial void LogTablePrivilegesGrantingToNewTable(ObjectReference owner, SqlIdentifier role);
}
