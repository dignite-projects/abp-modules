using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// <see cref="IFlexFieldRepository{TField}"/> over a downstream's own field-definition entity. The kernel
/// never calls these itself - they exist so a downstream does not have to write the same three queries.
/// </summary>
public class FlexFieldRepository_Tests : FlexFieldsMongoDbTestBase
{
    private IFlexFieldRepository<TestField> Repository => GetRequiredService<IFlexFieldRepository<TestField>>();

    [Fact]
    public async Task FindByName_returns_the_matching_field()
    {
        await InsertFieldsAsync("Title", "Summary");

        var field = await WithResultAsync(() => Repository.FindByNameAsync("Summary"));

        field.ShouldNotBeNull();
        field!.Name.ShouldBe("Summary");
    }

    [Fact]
    public async Task FindByName_returns_null_when_there_is_no_such_field()
    {
        await InsertFieldsAsync("Title");

        (await WithResultAsync(() => Repository.FindByNameAsync("Nope"))).ShouldBeNull();
    }

    [Fact]
    public async Task GetList_returns_the_fields_with_the_given_ids()
    {
        var ids = await InsertFieldsAsync("Title", "Summary", "Body");
        var wanted = new[] { ids[0], ids[2] };

        var fields = await WithResultAsync(() => Repository.GetListAsync(wanted));

        fields.Select(f => f.Id).ShouldBe(wanted, ignoreOrder: true);
    }

    [Fact]
    public async Task NameExists_reports_whether_the_name_is_taken()
    {
        await InsertFieldsAsync("Title");

        (await WithResultAsync(() => Repository.NameExistsAsync("Title"))).ShouldBeTrue();
        (await WithResultAsync(() => Repository.NameExistsAsync("Nope"))).ShouldBeFalse();
    }

    [Fact]
    public async Task NameExists_can_exclude_the_field_being_renamed()
    {
        var ids = await InsertFieldsAsync("Title");

        // The check a downstream makes before renaming: "is this name taken by anyone other than me?"
        (await WithResultAsync(() => Repository.NameExistsAsync("Title", excludedId: ids[0]))).ShouldBeFalse();
    }

    private async Task<List<Guid>> InsertFieldsAsync(params string[] names)
    {
        var ids = new List<Guid>();

        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetDbContextAsync();

            foreach (var name in names)
            {
                var field = new TestField(Guid.NewGuid(), name, TextFieldType.ControlName);
                ids.Add(field.Id);
                await dbContext.Fields.InsertOneAsync(field);
            }
        });

        return ids;
    }

    private async Task<TResult> WithResultAsync<TResult>(Func<Task<TResult>> action)
    {
        TResult result = default!;
        await WithUnitOfWorkAsync(async () => result = await action());
        return result;
    }
}
