using NSchema.Model;
using NSchema.Model.Routines;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// The <see cref="Address"/> contract: the addresses are the containment shapes, they key by value across
/// kinds, and a same-named address at a different containment depth is a different address.
/// </summary>
public class AddressTests
{
    public static TheoryData<Address> Addresses() =>
    [
        new ObjectAddress("app", "users"),
        new MemberAddress("app", "users", "email"),
        new ScopedAddress("app", "seed"),
        new ScopedAddress(null, "seed"),
    ];

    [Theory]
    [MemberData(nameof(Addresses))]
    public void Address_KeysByValue(Address address)
    {
        // Arrange — an address is a dictionary key (the dependency graph nodes on it), so an equal address
        // must find the entry an identical one stored.
        var keyed = new Dictionary<Address, string> { [address] = "node" };

        // Assert
        keyed.ShouldContainKey(address with { });
    }

    [Theory]
    [MemberData(nameof(Addresses))]
    public void ToString_RendersTheAddressValue(Address address)
    {
        // The written form is Value; ToString is sealed onto it so display never drifts from the contract.
        address.ToString().ShouldBe(address.Value);
    }

    [Fact]
    public void Addresses_AtDifferentContainmentDepths_AreNeverEqual()
    {
        // Arrange — the graph keys every kind of node in one dictionary, so shapes must not collide.
        var scoped = new ScopedAddress("app", "users");
        var obj = new ObjectAddress("app", "users");

        // Assert — both render 'app.users', but they address different things.
        scoped.Value.ShouldBe(obj.Value);
        ((Address)scoped).ShouldNotBe(obj);
    }

    [Fact]
    public void RoutineReference_IsNotAnAddress()
    {
        // A reference as written is not an address: an unqualified one is resolved by the engine's search
        // path, so it identifies nothing on its own.
        typeof(Address).IsAssignableFrom(typeof(RoutineReference)).ShouldBeFalse();
    }
}
