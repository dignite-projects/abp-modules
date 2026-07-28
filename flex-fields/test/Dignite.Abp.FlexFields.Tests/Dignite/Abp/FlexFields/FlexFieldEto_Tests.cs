using System;
using Shouldly;
using Volo.Abp.EventBus;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// The field-lifecycle ETOs. Their event names are a wire contract - a subscriber in another process binds
/// to the string, not the type - so the names are pinned here as literals: renaming or moving a class must
/// fail this test rather than silently stop matching live subscribers.
/// </summary>
public class FlexFieldEto_Tests
{
    [Fact]
    public void Renamed_eto_keeps_its_wire_name()
    {
        EventNameAttribute.GetNameOrDefault<FlexFieldRenamedEto>()
            .ShouldBe("Dignite.Abp.FlexFields.FlexFieldRenamed");
    }

    [Fact]
    public void Deleted_eto_keeps_its_wire_name()
    {
        EventNameAttribute.GetNameOrDefault<FlexFieldDeletedEto>()
            .ShouldBe("Dignite.Abp.FlexFields.FlexFieldDeleted");
    }

    [Fact]
    public void Etos_carry_a_tenant_id_for_the_event_bus_to_restore()
    {
        // EventBusBase reads IMultiTenant.TenantId to pick the tenant a handler runs in. Without the
        // interface it falls back to whatever tenant the handler happens to land in - which for a deferred
        // or distributed event is the host, and would migrate the wrong bags.
        new FlexFieldRenamedEto().ShouldBeAssignableTo<IMultiTenant>();
        new FlexFieldDeletedEto().ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void Renamed_eto_round_trips_through_its_constructor()
    {
        var fieldId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var eto = new FlexFieldRenamedEto(fieldId, "AuthorName", "Author", tenantId);

        eto.FieldId.ShouldBe(fieldId);
        eto.OldName.ShouldBe("AuthorName");
        eto.NewName.ShouldBe("Author");
        eto.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Deleted_eto_round_trips_through_its_constructor()
    {
        var fieldId = Guid.NewGuid();

        var eto = new FlexFieldDeletedEto(fieldId, "AuthorName");

        eto.FieldId.ShouldBe(fieldId);
        eto.Name.ShouldBe("AuthorName");
        // Host-level definition - null is a real value here, not a missing one.
        eto.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Etos_have_a_parameterless_constructor_for_deserialization()
    {
        // A distributed subscriber materializes these from JSON, which needs the default constructor to
        // survive the convenience overloads above.
        typeof(FlexFieldRenamedEto).GetConstructor(Type.EmptyTypes).ShouldNotBeNull();
        typeof(FlexFieldDeletedEto).GetConstructor(Type.EmptyTypes).ShouldNotBeNull();
    }
}
