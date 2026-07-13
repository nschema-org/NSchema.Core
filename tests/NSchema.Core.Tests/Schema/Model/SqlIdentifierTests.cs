using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Tests.Schema.Model;

/// <summary>
/// The identifier contract: case-insensitive identity, written-casing preservation, and the collection
/// behaviors (hashing, ordering) that follow from it.
/// </summary>
public class SqlIdentifierTests
{
    [Fact]
    public void Equals_CaseVariants_AreTheSameIdentifier()
    {
        // Arrange
        SqlIdentifier lower = new SqlIdentifier("users");
        SqlIdentifier mixed = new SqlIdentifier("Users");

        // Assert
        lower.Equals(mixed).ShouldBeTrue();
        (lower == mixed).ShouldBeTrue();
        (lower != mixed).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentNames_AreNotEqual()
    {
        // Arrange
        SqlIdentifier users = new SqlIdentifier("users");
        SqlIdentifier orders = new SqlIdentifier("orders");

        // Assert
        (users == orders).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_CaseVariants_Collide()
    {
        // Assert
        new SqlIdentifier("users").GetHashCode().ShouldBe(new SqlIdentifier("USERS").GetHashCode());
    }

    [Fact]
    public void Value_PreservesWrittenCasing()
    {
        // Arrange
        SqlIdentifier identifier = new SqlIdentifier("MyTable");

        // Assert
        identifier.Value.ShouldBe("MyTable");
        identifier.ToString().ShouldBe("MyTable");
    }

    [Fact]
    public void RecordEquality_NestedIdentifierLists_AreCaseInsensitive()
    {
        // Arrange — the latent-spurious-diff class this type fixes: referenced column lists differing only in case.
        var current = new PrimaryKey(new SqlIdentifier("pk"), [new SqlIdentifier("ID"), new SqlIdentifier("Email")]);
        var desired = new PrimaryKey(new SqlIdentifier("PK"), [new SqlIdentifier("id"), new SqlIdentifier("email")]);

        // Assert
        current.Equals(desired).ShouldBeTrue();
    }

    [Fact]
    public void HashSet_CaseVariants_AreOneEntry()
    {
        // Act
        var set = new HashSet<SqlIdentifier> { new SqlIdentifier("users"), new SqlIdentifier("Users"), new SqlIdentifier("USERS") };

        // Assert
        set.Count.ShouldBe(1);
    }

    [Fact]
    public void CompareTo_OrdersCaseInsensitively()
    {
        // Arrange
        var names = new List<SqlIdentifier> { new SqlIdentifier("Zebra"), new SqlIdentifier("apple"), new SqlIdentifier("Mango") };

        // Act
        names.Sort();

        // Assert
        names.Select(n => n.Value).ShouldBe(["apple", "Mango", "Zebra"]);
    }
}
