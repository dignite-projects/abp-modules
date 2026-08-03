using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Boolean;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// <see cref="IFlexFieldValidator{TEntity}"/> is provider-agnostic (it lives in <c>.Domain</c> and touches
/// no database), but it needs a host entity and an <see cref="IFlexFieldProvider{TEntity}"/> to run
/// against - which this project already provides - so its tests live here rather than in a project of
/// their own.
/// </summary>
public class FlexFieldValidator_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    private TestArticleFlexFieldProvider Provider => GetRequiredService<TestArticleFlexFieldProvider>();

    private IFlexFieldValidator<TestArticle> Validator => GetRequiredService<IFlexFieldValidator<TestArticle>>();

    [Fact]
    public async Task Valid_values_produce_no_errors()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName, required: true);
        Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);

        var article = new TestArticle(Guid.NewGuid(), "Host");
        article.SetField("Title", "A perfectly good title");
        article.SetField("ViewCount", 42);

        (await Validator.ValidateAsync(article)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_required_field_with_no_value_is_reported_against_that_field()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName, required: true);

        var article = new TestArticle(Guid.NewGuid(), "Host");

        var error = (await Validator.ValidateAsync(article)).ShouldHaveSingleItem();

        // The member name is what lets a UI attach the message to the right input.
        error.MemberNames.ShouldBe(new[] { "Title" });
        error.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        // Localization really resolved - not left as the raw key.
        error.ErrorMessage.ShouldNotBe("Validate:Required");
        error.ErrorMessage.ShouldContain("Title");
    }

    [Fact]
    public async Task A_non_required_field_with_no_value_is_fine()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName, required: false);

        var article = new TestArticle(Guid.NewGuid(), "Host");

        (await Validator.ValidateAsync(article)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Text_over_its_character_limit_is_reported()
    {
        var usage = Provider.AddDefinition("Title", TextFieldType.ControlName);
        new TextConfiguration(usage.Field.Configuration) { CharLimit = 5 };

        var article = new TestArticle(Guid.NewGuid(), "Host");
        article.SetField("Title", "far too long");

        var error = (await Validator.ValidateAsync(article)).ShouldHaveSingleItem();
        error.MemberNames.ShouldBe(new[] { "Title" });
        error.ErrorMessage.ShouldContain("5");
    }

    [Fact]
    public async Task A_number_outside_its_configured_range_is_reported()
    {
        var usage = Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);
        new NumberConfiguration(usage.Field.Configuration) { Min = 10, Max = 100 };

        var article = new TestArticle(Guid.NewGuid(), "Host");
        article.SetField("ViewCount", 500);

        (await Validator.ValidateAsync(article)).ShouldHaveSingleItem()
            .MemberNames.ShouldBe(new[] { "ViewCount" });
    }

    [Fact]
    public async Task A_value_that_is_not_of_the_fields_type_is_reported()
    {
        Provider.AddDefinition("ViewCount", NumberFieldType.ControlName);

        var article = new TestArticle(Guid.NewGuid(), "Host");
        article.SetField("ViewCount", "not a number");

        (await Validator.ValidateAsync(article)).ShouldHaveSingleItem()
            .MemberNames.ShouldBe(new[] { "ViewCount" });
    }

    [Fact]
    public async Task A_selection_outside_the_configured_options_is_reported()
    {
        var usage = Provider.AddDefinition("Tags", SelectFieldType.ControlName);
        new SelectConfiguration(usage.Field.Configuration)
        {
            Options = new List<SelectListItem> { new("Red", "red", false), new("Blue", "blue", false) }
        };

        var article = new TestArticle(Guid.NewGuid(), "Host");
        article.SetField("Tags", new List<string> { "red", "chartreuse" });

        (await Validator.ValidateAsync(article)).ShouldHaveSingleItem()
            .MemberNames.ShouldBe(new[] { "Tags" });
    }

    [Fact]
    public async Task Every_field_is_validated_rather_than_stopping_at_the_first_failure()
    {
        Provider.AddDefinition("Title", TextFieldType.ControlName, required: true);
        Provider.AddDefinition("Summary", TextFieldType.ControlName, required: true);
        Provider.AddDefinition("ViewCount", NumberFieldType.ControlName, required: true);

        // Nothing set at all: a form should hear about all three problems in one round trip.
        var article = new TestArticle(Guid.NewGuid(), "Host");

        var errors = await Validator.ValidateAsync(article);

        errors.Count.ShouldBe(3);
        errors.SelectMany(e => e.MemberNames)
            .ShouldBe(new[] { "Title", "Summary", "ViewCount" }, ignoreOrder: true);
    }

    [Fact]
    public async Task A_field_type_with_no_rules_never_reports_anything()
    {
        // BooleanFieldType.Validate returns empty unconditionally - the validator must cope with a field
        // type that contributes nothing, including when the value is absent.
        Provider.AddDefinition("IsFeatured", BooleanFieldType.ControlName, required: true);

        var article = new TestArticle(Guid.NewGuid(), "Host");

        (await Validator.ValidateAsync(article)).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_entity_with_no_configured_fields_validates_clean()
    {
        var article = new TestArticle(Guid.NewGuid(), "Host");
        article.SetField("Stray", "left over from a deleted field");

        // Values with no matching definition are simply not the validator's business.
        (await Validator.ValidateAsync(article)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Searchable_has_no_bearing_on_validation()
    {
        // Searchable governs indexing only; a non-searchable field is still validated.
        Provider.AddDefinition("Notes", TextFieldType.ControlName, searchable: false, required: true);

        var article = new TestArticle(Guid.NewGuid(), "Host");

        (await Validator.ValidateAsync(article)).ShouldHaveSingleItem()
            .MemberNames.ShouldBe(new[] { "Notes" });
    }
}
