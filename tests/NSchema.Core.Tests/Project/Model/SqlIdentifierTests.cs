using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The identifier contract: identity is the exact written text — case-sensitive equality, written-casing
/// preservation, and the collection behaviors (hashing, ordering) that follow from it.
/// </summary>
public class SqlIdentifierTests
{
    [Fact]
    public void Equals_CaseVariants_AreDifferentIdentifiers()
    {
        // Arrange
        var lower = new SqlIdentifier("users");
        var mixed = new SqlIdentifier("Users");

        // Assert
        lower.Equals(mixed).ShouldBeFalse();
        (lower == mixed).ShouldBeFalse();
        (lower != mixed).ShouldBeTrue();
    }

    [Fact]
    public void Equals_SameText_AreEqual()
    {
        // Assert
        (new SqlIdentifier("users") == new SqlIdentifier("users")).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentNames_AreNotEqual()
    {
        // Arrange
        var users = new SqlIdentifier("users");
        var orders = new SqlIdentifier("orders");

        // Assert
        (users == orders).ShouldBeFalse();
    }

    [Fact]
    public void Value_PreservesWrittenCasing()
    {
        // Arrange
        var identifier = new SqlIdentifier("MyTable");

        // Assert
        identifier.Value.ShouldBe("MyTable");
        identifier.ToString().ShouldBe("MyTable");
    }

    [Fact]
    public void RecordEquality_NestedIdentifierLists_AreCaseSensitive()
    {
        // Arrange — column lists differing only in case name different columns.
        var current = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("ID"), new SqlIdentifier("Email")] };
        var desired = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id"), new SqlIdentifier("email")] };

        // Assert
        current.Equals(desired).ShouldBeFalse();
        current.Equals(new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("ID"), new SqlIdentifier("Email")] }).ShouldBeTrue();
    }

    [Fact]
    public void HashSet_CaseVariants_AreSeparateEntries()
    {
        // Act
        var set = new HashSet<SqlIdentifier> { new SqlIdentifier("users"), new SqlIdentifier("Users"), new SqlIdentifier("USERS") };

        // Assert
        set.Count.ShouldBe(3);
    }

    [Fact]
    public void CompareTo_OrdersOrdinally()
    {
        // Arrange
        var names = new List<SqlIdentifier> { new SqlIdentifier("zebra"), new SqlIdentifier("Mango"), new SqlIdentifier("apple") };

        // Act
        names.Sort();

        // Assert — ordinal order: uppercase sorts before lowercase.
        names.Select(n => n.Value).ShouldBe(["Mango", "apple", "zebra"]);
    }
}
