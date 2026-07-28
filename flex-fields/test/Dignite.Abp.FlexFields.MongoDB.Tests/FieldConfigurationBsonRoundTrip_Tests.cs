using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Select;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// <see cref="FlexFieldsMongoDbModule"/> registers the same custom bag serializer for
/// <see cref="FieldConfigurationDictionary"/> as it does for <see cref="FlexFieldDictionary"/>, but a field
/// definition's configuration commonly holds more than scalars - <see cref="SelectConfiguration.Options"/> is
/// a <c>List&lt;SelectListItem&gt;</c>, a list of a plain class, not a list of strings or numbers. This is
/// what actually exercises the serializer's <c>default:</c> (POCO-via-<c>ObjectSerializer</c>) fallback
/// nested inside its array-writing path, which nothing else in this test project reaches.
/// </summary>
public class FieldConfigurationBsonRoundTrip_Tests : FlexFieldsMongoDbTestBase
{
    [Fact]
    public async Task A_list_of_options_round_trips_through_the_bag_serializer()
    {
        var field = new TestField(System.Guid.NewGuid(), "Tags", SelectFieldType.ControlName);
        _ = new SelectConfiguration(field.Configuration)
        {
            NullText = "None",
            Multiple = true,
            Options = new List<SelectListItem>
            {
                new("Red", "red", selected: false),
                new("Blue", "blue", selected: true)
            }
        };

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            await dbContext.Fields.InsertOneAsync(field);
        });

        TestField reloaded = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();
            reloaded = await dbContext.Fields.Find(f => f.Id == field.Id).SingleAsync();
        });

        var reloadedConfiguration = new SelectConfiguration(reloaded.Configuration);
        reloadedConfiguration.NullText.ShouldBe("None");
        reloadedConfiguration.Multiple.ShouldBeTrue();
        // Against a literal, not against configuration.Options re-read through the same GetConfiguration<T>
        // path under test - comparing against a second live read of the same kind would let a defect that
        // corrupts both reads identically (e.g. always dropping Selected) still pass.
        reloadedConfiguration.Options.Select(o => (o.Text, o.Value, o.Selected))
            .ShouldBe(new[] { ("Red", "red", false), ("Blue", "blue", true) });
    }
}
