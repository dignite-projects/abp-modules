using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// <see cref="IFlexFieldRepository{TField}"/> through its EF Core base
/// (<see cref="EfCoreFlexFieldRepositoryBase{TDbContext,TField}"/>), resolved the way a downstream would:
/// via the kernel's repository interface, not the concrete <see cref="TestFieldRepository"/> type.
/// </summary>
public class FlexFieldRepository_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    private IFlexFieldRepository<TestField> Repository => GetRequiredService<IFlexFieldRepository<TestField>>();

    [Fact]
    public async Task FindByNameAsync_returns_the_matching_field()
    {
        await InsertFieldAsync("Title");

        await WithUnitOfWorkAsync(async () =>
        {
            var field = await Repository.FindByNameAsync("Title");

            field.ShouldNotBeNull();
            field!.Name.ShouldBe("Title");
        });
    }

    [Fact]
    public async Task FindByNameAsync_returns_null_when_no_field_has_that_name()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            (await Repository.FindByNameAsync("NoSuchField")).ShouldBeNull();
        });
    }

    [Fact]
    public async Task GetListAsync_by_ids_returns_only_the_requested_fields()
    {
        var wantedId = await InsertFieldAsync("Title");
        await InsertFieldAsync("Body");
        var otherWantedId = await InsertFieldAsync("Summary");

        await WithUnitOfWorkAsync(async () =>
        {
            var fields = await Repository.GetListAsync(new[] { wantedId, otherWantedId });

            fields.Select(f => f.Name).ShouldBe(new[] { "Title", "Summary" }, ignoreOrder: true);
        });
    }

    [Fact]
    public async Task NameExistsAsync_is_true_for_an_existing_name()
    {
        await InsertFieldAsync("Title");

        await WithUnitOfWorkAsync(async () =>
        {
            (await Repository.NameExistsAsync("Title")).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task NameExistsAsync_is_false_when_excluding_the_only_field_with_that_name()
    {
        var fieldId = await InsertFieldAsync("Title");

        await WithUnitOfWorkAsync(async () =>
        {
            (await Repository.NameExistsAsync("Title", excludedId: fieldId)).ShouldBeFalse();
        });
    }

    private async Task<Guid> InsertFieldAsync(string name)
    {
        var fieldId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await Repository.InsertAsync(new TestField(fieldId, name, TextFieldType.ControlName));
        });

        return fieldId;
    }
}
