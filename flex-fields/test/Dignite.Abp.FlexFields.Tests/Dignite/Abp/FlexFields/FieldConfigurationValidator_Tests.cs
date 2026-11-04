using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Dignite.Abp.FlexFields.CKEditor;
using Dignite.Abp.FlexFields.Localization;
using Dignite.Abp.FlexFields.Matrix;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Table;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.Tree;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp.Localization;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// <see cref="IFieldConfigurationValidator"/> against the real registered field types: a configuration is
/// checked against what its field type declares, and a field type that declares nothing is not checked at all.
/// Configurations are built the way they reach a host - parsed from the JSON a client sends, so every value is a
/// <see cref="JsonElement"/> - and the messages are asserted in English, since a message is what an AI client
/// corrects itself from.
/// </summary>
public class FieldConfigurationValidator_Tests : DigniteAbpFlexFieldsTestBase
{
    private readonly IFieldConfigurationValidator _validator;
    private readonly IFieldTypeResolver _resolver;
    private readonly IDisposable _culture;

    public FieldConfigurationValidator_Tests()
    {
        // Whatever the machine's language is, the messages asserted below are the English ones.
        _culture = CultureHelper.Use("en");

        _validator = GetRequiredService<IFieldConfigurationValidator>();
        _resolver = GetRequiredService<IFieldTypeResolver>();
    }

    public override void Dispose()
    {
        _culture.Dispose();
        base.Dispose();
    }

    // ---- unknown keys ------------------------------------------------------------------------------------

    [Fact]
    public void An_unknown_key_is_rejected_naming_the_key_the_field_type_and_every_valid_key()
    {
        // The case that started this: a model passed Text.Multiline, which does not exist, and was told nothing.
        var exception = Should.Throw<AbpValidationException>(
            () => _validator.Check(TextFieldType.ControlName, Json("""{ "Text.Multiline": true }""")));

        var error = exception.ValidationErrors.ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe(
            "'Text.Multiline' is not a configuration key of the Text field type. " +
            "The valid keys are: 'Text.Mode', 'Text.CharLimit', 'Text.Placeholder'.");
        error.MemberNames.ShouldBe(new[] { "Configuration[Text.Multiline]" });

        // The exception message carries the same, for a client that never looks at the structured errors.
        exception.Message.ShouldContain("'Text.Multiline'");
        exception.Message.ShouldContain("'Text.CharLimit'");
    }

    [Fact]
    public void A_key_that_differs_only_in_case_is_rejected_with_the_spelling_that_works()
    {
        var error = _validator.Validate(TextFieldType.ControlName, Json("""{ "text.mode": 1 }""")).ShouldHaveSingleItem();

        error.ErrorMessage!.ShouldContain("'text.mode' is not a configuration key");
        error.ErrorMessage.ShouldContain("did you mean 'Text.Mode'?");
    }

    [Fact]
    public void A_key_of_another_field_type_is_unknown_to_this_one()
    {
        var error = _validator.Validate(TextFieldType.ControlName, Json("""{ "Number.Min": 1 }""")).ShouldHaveSingleItem();

        error.ErrorMessage!.ShouldStartWith("'Number.Min' is not a configuration key of the Text field type.");
    }

    [Fact]
    public void Every_problem_is_reported_in_one_go()
    {
        var exception = Should.Throw<AbpValidationException>(() => _validator.Check(
            TextFieldType.ControlName,
            Json("""{ "Text.Multiline": true, "Text.Wrap": "soft", "Text.CharLimit": "many" }""")));

        exception.ValidationErrors.Count.ShouldBe(3);
        exception.Message.ShouldContain("'Text.Multiline'");
        exception.Message.ShouldContain("'Text.Wrap'");
        exception.Message.ShouldContain("'Text.CharLimit'");
    }

    [Fact]
    public void A_message_is_localized()
    {
        using var _ = CultureHelper.Use("zh-Hans");

        var error = _validator.Validate(TextFieldType.ControlName, Json("""{ "Text.Multiline": true }""")).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldBe("“Text.Multiline”不是 Text 字段类型的配置键。有效的键：'Text.Mode', 'Text.CharLimit', 'Text.Placeholder'。");
    }

    // ---- values of the wrong kind ------------------------------------------------------------------------

    public static IEnumerable<object[]> WrongValues() => new[]
    {
        new object[] { "Text", "Text.CharLimit", "\"many\"" },
        new object[] { "Text", "Text.CharLimit", "12.5" },
        new object[] { "Text", "Text.CharLimit", "true" },
        // An enum is stored by number: "MultipleLine" would not read back as TextMode.MultipleLine.
        new object[] { "Text", "Text.Mode", "\"MultipleLine\"" },
        new object[] { "Text", "Text.Mode", "7" },
        new object[] { "Text", "Text.Placeholder", "5" },
        new object[] { "Number", "Number.Min", "\"low\"" },
        new object[] { "Number", "Number.Max", "[1]" },
        // An empty string does not mean "unset" here: it does not read back as a number of decimal places.
        new object[] { "Number", "Number.Decimals", "\"\"" },
        new object[] { "DateTime", "DateTime.Min", "\"tomorrow\"" },
        new object[] { "DateTime", "DateTime.Min", "20260131" },
        new object[] { "DateTime", "DateTime.InputMode", "\"Month\"" },
        new object[] { "Select", "Select.Multiple", "\"yes\"" },
        // The likeliest mistake of all: plain strings where option objects were meant. Stored, it makes the
        // field unreadable.
        new object[] { "Select", "Select.Options", "[\"a\",\"b\"]" },
        new object[] { "Select", "Select.Options", "{\"a\":1}" },
        new object[] { "Select", "Select.Options", "[{\"text\":\"A\",\"value\":1}]" },
        new object[] { "Boolean", "Boolean.Default", "\"maybe\"" },
        new object[] { "Tree", "Tree.Nodes", "\"x\"" },
        new object[] { "Tree", "Tree.Nodes", "[{\"text\":\"A\",\"value\":\"a\",\"children\":\"none\"}]" },
        new object[] { "Matrix", "Matrix.BlockTypes", "\"x\"" },
        new object[] { "Table", "Table.Columns", "[\"a\"]" }
    };

    [Theory]
    [MemberData(nameof(WrongValues))]
    public void A_value_of_the_wrong_kind_is_rejected(string fieldTypeName, string key, string valueJson)
    {
        var configuration = Json($"{{ \"{key}\": {valueJson} }}");

        var error = _validator.Validate(fieldTypeName, configuration).ShouldHaveSingleItem();

        error.MemberNames.ShouldBe(new[] { $"Configuration[{key}]" });
        error.ErrorMessage!.ShouldStartWith($"The value of '{key}' must be ");
    }

    [Fact]
    public void A_wrong_value_message_says_what_was_expected_and_what_was_given()
    {
        _validator.Validate(TextFieldType.ControlName, Json("""{ "Text.Mode": "MultipleLine" }""")).ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe(
                "The value of 'Text.Mode' must be an integer, one of 0 (SingleLine) | 1 (MultipleLine), but it is \"MultipleLine\".");

        _validator.Validate(SelectFieldType.ControlName, Json("""{ "Select.Options": ["a"] }""")).ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe(
                "The value of 'Select.Options' must be an array of the form " +
                "[{ \"text\": string, \"value\": string, \"selected\": boolean }], but it is [\"a\"].");
    }

    // ---- valid configurations ----------------------------------------------------------------------------

    public static IEnumerable<object[]> ValidValues() => new[]
    {
        new object[] { "Text", "Text.CharLimit", "2000" },
        new object[] { "Text", "Text.CharLimit", "null" },
        new object[] { "Text", "Text.Mode", "1" },
        new object[] { "Text", "Text.Placeholder", "\"\"" },
        new object[] { "Number", "Number.Min", "-1.5" },
        // The Angular Number editor's inputs are text boxes: what it saves is the text typed.
        new object[] { "Number", "Number.Min", "\"10\"" },
        new object[] { "Number", "Number.Max", "\"\"" },
        new object[] { "Number", "Number.Decimals", "\"3\"" },
        new object[] { "Number", "Number.Step", "null" },
        new object[] { "Number", "FormatSpecifier", "\"N2\"" },
        new object[] { "Number", "Number.Placeholder", "\"\"" },
        new object[] { "DateTime", "DateTime.InputMode", "2" },
        // What the date, date-time and month inputs of the Angular editor produce.
        new object[] { "DateTime", "DateTime.Min", "\"2026-01-31\"" },
        new object[] { "DateTime", "DateTime.Min", "\"2026-01-31T10:00\"" },
        new object[] { "DateTime", "DateTime.Max", "\"2026-01\"" },
        new object[] { "DateTime", "DateTime.Max", "\"\"" },
        new object[] { "Select", "Select.Options", "[]" },
        // Property names in PascalCase, as the Angular editor's form groups send them.
        new object[] { "Select", "Select.Options", "[{\"Text\":\"A\",\"Value\":\"a\",\"Selected\":false}]" },
        new object[] { "Select", "Select.Multiple", "true" },
        new object[] { "Select", "Select.Multiple", "\"true\"" },
        new object[] { "Select", "Select.Size", "null" },
        new object[] { "Boolean", "Boolean.Default", "false" },
        new object[] { "Tree", "Tree.Nodes", "[{\"text\":\"A\",\"value\":\"a\",\"selected\":false,\"children\":[{\"text\":\"B\",\"value\":\"b\"}]}]" },
        new object[] { "Matrix", "Matrix.BlockTypes", "[]" },
        new object[] { "Table", "Table.Columns", "[{\"name\":\"n\",\"displayName\":\"N\",\"fieldTypeName\":\"Number\",\"required\":true,\"configuration\":{\"Number.Min\":\"1\"}}]" }
    };

    [Theory]
    [MemberData(nameof(ValidValues))]
    public void A_valid_value_is_accepted(string fieldTypeName, string key, string valueJson)
    {
        _validator.Validate(fieldTypeName, Json($"{{ \"{key}\": {valueJson} }}")).ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_configuration_is_accepted_for_every_field_type()
    {
        foreach (var fieldType in _resolver.GetAll().Where(fieldType => fieldType.ConfigurationKeys?.Any(key => key.Required) != true))
        {
            _validator.Validate(fieldType.Name, new FieldConfigurationDictionary()).ShouldBeEmpty(fieldType.Name);
            _validator.Validate(fieldType.Name, null).ShouldBeEmpty(fieldType.Name);
        }
    }

    [Fact]
    public void A_configuration_built_in_code_through_the_typed_wrappers_is_accepted()
    {
        // The other way a configuration arrives: not as parsed JSON but as the objects a seeder or a test sets.
        var text = new FieldConfigurationDictionary();
        _ = new TextConfiguration(text) { Mode = TextMode.MultipleLine, CharLimit = 2000, Placeholder = "Say something" };
        _validator.Validate(TextFieldType.ControlName, text).ShouldBeEmpty();

        var select = new FieldConfigurationDictionary();
        _ = new SelectConfiguration(select)
        {
            Options = new List<SelectListItem> { new("Red", "red", true), new("Blue", "blue", false) },
            Multiple = true,
            NullText = "None"
        };
        _validator.Validate(SelectFieldType.ControlName, select).ShouldBeEmpty();

        var tree = new FieldConfigurationDictionary();
        _ = new TreeConfiguration(tree)
        {
            Nodes = new List<TreeNodeItem> { new("Root", "root", false) { Children = { new TreeNodeItem("Leaf", "leaf", false) } } }
        };
        _validator.Validate(TreeFieldType.ControlName, tree).ShouldBeEmpty();

        var number = new FieldConfigurationDictionary();
        _ = new Number.NumberConfiguration(number) { Min = 1, Max = 10.5m, Decimals = 1, Placeholder = "0" };
        _validator.Validate(Number.NumberFieldType.ControlName, number).ShouldBeEmpty();
    }

    // ---- a field type that says nothing ------------------------------------------------------------------

    [Fact]
    public void A_field_type_that_declares_nothing_is_not_validated()
    {
        var configuration = Json("""{ "Anything": 1, "Goes.Here": [true], "even": { "nested": "stuff" } }""");

        _resolver.Get(UndeclaredConfigurationFieldType.ControlName).ConfigurationKeys.ShouldBeNull();

        _validator.Validate(UndeclaredConfigurationFieldType.ControlName, configuration).ShouldBeEmpty();
        Should.NotThrow(() => _validator.Check(UndeclaredConfigurationFieldType.ControlName, configuration));
    }

    [Fact]
    public void A_field_type_that_implements_the_interface_directly_declares_nothing_and_is_not_validated()
    {
        // Compiled against an IFieldType without the member: the default interface member answers for it.
        IFieldType fieldType = _resolver.Get(DirectInterfaceFieldType.ControlName);

        fieldType.ShouldBeOfType<DirectInterfaceFieldType>();
        fieldType.ConfigurationKeys.ShouldBeNull();
        _validator.Validate(fieldType.Name, Json("""{ "Whatever": 1 }""")).ShouldBeEmpty();
    }

    [Fact]
    public void A_field_type_that_declares_an_empty_list_takes_no_configuration()
    {
        // Declaring nothing and declaring that there is nothing are different statements.
        _resolver.Get(NoConfigurationFieldType.ControlName).ConfigurationKeys.ShouldNotBeNull().ShouldBeEmpty();

        _validator.Validate(NoConfigurationFieldType.ControlName, new FieldConfigurationDictionary()).ShouldBeEmpty();

        _validator.Validate(NoConfigurationFieldType.ControlName, Json("""{ "Anything": 1 }""")).ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe("'Anything' is not a configuration key of the Test.NoConfiguration field type, which takes no configuration.");
    }

    [Fact]
    public void A_field_type_name_that_is_not_registered_is_not_checked()
    {
        _validator.Validate("Not.Registered", Json("""{ "Anything": 1 }""")).ShouldBeEmpty();
    }

    // ---- required keys, and clearing a key ---------------------------------------------------------------

    [Theory]
    [InlineData("{ }")]
    [InlineData("""{ "Test.RequiredKey.Container": null }""")]
    [InlineData("""{ "Test.RequiredKey.Container": "  " }""")]
    public void A_required_key_that_is_missing_or_empty_is_reported(string configurationJson)
    {
        _validator.Validate(RequiredKeyFieldType.ControlName, Json(configurationJson)).ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe("'Test.RequiredKey.Container' is required by the Test.RequiredKey field type.");
    }

    [Fact]
    public void A_required_key_that_is_present_is_accepted()
    {
        _validator.Validate(RequiredKeyFieldType.ControlName, Json("""{ "Test.RequiredKey.Container": "images" }""")).ShouldBeEmpty();
    }

    [Fact]
    public void A_key_whose_empty_string_means_unset_can_be_cleared_but_a_malformed_value_is_still_rejected()
    {
        FieldConfigurationDictionary WithId(string idJson) =>
            Json("{ \"Test.RequiredKey.Container\": \"images\", \"Test.RequiredKey.Id\": " + idJson + " }");

        _validator.Validate(RequiredKeyFieldType.ControlName, WithId("\"\"")).ShouldBeEmpty();
        _validator.Validate(RequiredKeyFieldType.ControlName, WithId("null")).ShouldBeEmpty();
        _validator.Validate(RequiredKeyFieldType.ControlName, WithId("\"" + Guid.NewGuid() + "\"")).ShouldBeEmpty();
        _validator.Validate(RequiredKeyFieldType.ControlName, WithId("\"nope\"")).ShouldHaveSingleItem();
    }

    // ---- stored data -------------------------------------------------------------------------------------

    [Fact]
    public void Configuration_stored_before_a_key_was_declared_still_reads()
    {
        // Validation is a write-side check. A definition saved long ago, with a key nobody declares, or a value
        // of a kind nobody accepts any more, has to keep being read and used exactly as it was.
        var stored = Json("""{ "Text.CharLimit": 12, "Text.Multiline": true, "Legacy.Setting": [1, 2] }""");

        var textType = _resolver.Get(TextFieldType.ControlName);
        var configuration = (TextConfiguration)textType.GetConfiguration(stored);

        configuration.CharLimit.ShouldBe(12);
        configuration.Mode.ShouldBe(TextMode.SingleLine);

        var field = FlexFieldDataTestExtensions.Create("title", TextFieldType.ControlName);
        field.Configuration = stored;

        textType.Validate(new FieldValidationArgs(field.ToValue("a value that is too long"))).ShouldHaveSingleItem();
        textType.Validate(new FieldValidationArgs(field.ToValue("short"))).ShouldBeEmpty();

        // ... while the same configuration, offered again for saving as a new definition, is the thing that gets
        // refused - and offered again on an update that did not touch it, is not (see the next section).
        _validator.Validate(TextFieldType.ControlName, stored).Count.ShouldBe(2);
        _validator.Validate(TextFieldType.ControlName, stored, stored).ShouldBeEmpty();
    }

    // ---- an update: what the client did not change is not the client's to answer for ---------------------

    [Fact]
    public void A_stored_configuration_sent_back_unchanged_is_accepted_however_it_is_formatted()
    {
        var stored = Json("""{ "Text.CharLimit": 12, "Text.Multiline": true, "Legacy": { "a": [1, 2] } }""");
        var sentBack = Json("""{"Legacy":{"a":[1,2]},"Text.Multiline":true,"Text.CharLimit":12}""");

        _validator.Validate(TextFieldType.ControlName, sentBack, stored).ShouldBeEmpty();
        Should.NotThrow(() => _validator.Check(TextFieldType.ControlName, sentBack, stored));
    }

    [Fact]
    public void An_update_is_checked_for_whatever_the_client_added_or_changed()
    {
        var stored = Json("""{ "Text.CharLimit": 12, "Text.Multiline": true }""");
        var sent = Json("""{ "Text.CharLimit": "many", "Text.Multiline": true, "Text.Wrap": "soft" }""");

        var errors = _validator.Validate(TextFieldType.ControlName, sent, stored);

        errors.SelectMany(error => error.MemberNames).ShouldBe(
            new[] { "Configuration[Text.CharLimit]", "Configuration[Text.Wrap]" }, ignoreOrder: true);
    }

    [Fact]
    public void A_stored_value_of_the_wrong_kind_is_accepted_until_the_client_changes_it()
    {
        var stored = Json("""{ "Select.Options": ["a", "b"] }""");

        _validator.Validate(SelectFieldType.ControlName, Json("""{ "Select.Options": ["a", "b"] }"""), stored).ShouldBeEmpty();
        _validator.Validate(SelectFieldType.ControlName, Json("""{ "Select.Options": ["a", "c"] }"""), stored).ShouldHaveSingleItem();
    }

    [Fact]
    public void A_required_key_that_was_already_missing_is_not_the_updates_doing_but_one_it_removed_is()
    {
        var withoutIt = Json("""{ }""");
        var withIt = Json("""{ "Test.RequiredKey.Container": "images" }""");

        _validator.Validate(RequiredKeyFieldType.ControlName, withoutIt, withoutIt).ShouldBeEmpty();
        _validator.Validate(RequiredKeyFieldType.ControlName, withoutIt, withIt).ShouldHaveSingleItem();
        _validator.Validate(RequiredKeyFieldType.ControlName, withoutIt).ShouldHaveSingleItem();
    }

    [Fact]
    public void An_inline_field_that_is_as_it_was_is_not_checked_again_but_a_new_one_is()
    {
        var stored = Json("""
            { "Table.Columns": [ { "name": "a", "displayName": "A", "fieldTypeName": "Text", "configuration": { "Text.Multiline": true } } ] }
            """);
        var sent = Json("""
            { "Table.Columns": [
                { "name": "a", "displayName": "A", "fieldTypeName": "Text", "configuration": { "Text.Multiline": true } },
                { "name": "b", "displayName": "B", "fieldTypeName": "Text", "configuration": { "Text.Rows": 5 } } ] }
            """);

        _validator.Validate(TableFieldType.ControlName, stored, stored).ShouldBeEmpty();

        var error = _validator.Validate(TableFieldType.ControlName, sent, stored).ShouldHaveSingleItem();
        error.ErrorMessage!.ShouldContain("inline field 'b'");
        error.ErrorMessage.ShouldContain("'Text.Rows'");

        // As a new definition both are wrong.
        _validator.Validate(TableFieldType.ControlName, sent).Count.ShouldBe(2);
    }

    // ---- composite types ---------------------------------------------------------------------------------

    [Fact]
    public void An_inline_field_of_a_matrix_is_checked_against_its_own_field_type()
    {
        var configuration = Json("""
            {
              "Matrix.BlockTypes": [
                {
                  "name": "quote", "displayName": "Quote",
                  "fields": [
                    { "name": "text", "displayName": "Text", "fieldTypeName": "Text", "configuration": { "Text.Multiline": true } },
                    { "name": "author", "displayName": "Author", "fieldTypeName": "Text", "configuration": { "Text.CharLimit": 80 } }
                  ]
                }
              ]
            }
            """);

        var error = _validator.Validate(MatrixFieldType.ControlName, configuration).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldBe(
            "In the inline field 'text' (Text): 'Text.Multiline' is not a configuration key of the Text field type. " +
            "The valid keys are: 'Text.Mode', 'Text.CharLimit', 'Text.Placeholder'.");
        error.MemberNames.ShouldBe(new[] { "Configuration" });
    }

    [Fact]
    public void A_table_column_and_a_nested_composite_are_checked_too()
    {
        // Table > Matrix > Text: the unknown key is two levels down.
        var configuration = Json("""
            {
              "Table.Columns": [
                {
                  "name": "blocks", "displayName": "Blocks", "fieldTypeName": "Matrix",
                  "configuration": {
                    "Matrix.BlockTypes": [
                      { "name": "b", "displayName": "B", "fields": [
                        { "name": "t", "displayName": "T", "fieldTypeName": "Text", "configuration": { "Text.Rows": 5 } }
                      ] }
                    ]
                  }
                }
              ]
            }
            """);

        var error = _validator.Validate(TableFieldType.ControlName, configuration).ShouldHaveSingleItem();

        error.ErrorMessage!.ShouldContain("inline field 'blocks' (Matrix)");
        error.ErrorMessage.ShouldContain("inline field 't' (Text)");
        error.ErrorMessage.ShouldContain("'Text.Rows'");
    }

    [Fact]
    public void An_inline_field_of_a_type_that_is_not_registered_is_reported()
    {
        // A misspelled field type name is the same mistake as a misspelled key, and nothing else catches it
        // while the definition is being saved.
        var configuration = Json("""
            { "Table.Columns": [ { "name": "x", "displayName": "X", "fieldTypeName": "Textt", "configuration": { } } ] }
            """);

        var error = _validator.Validate(TableFieldType.ControlName, configuration).ShouldHaveSingleItem();

        error.ErrorMessage!.ShouldStartWith("The inline field 'x' has the field type 'Textt', which is not registered.");
        error.ErrorMessage.ShouldContain("'Text'");
        error.MemberNames.ShouldBe(new[] { "Configuration" });
    }

    [Fact]
    public void An_inline_field_of_a_type_that_has_since_been_removed_is_left_alone_on_an_update_that_did_not_touch_it()
    {
        var configuration = Json("""
            { "Table.Columns": [ { "name": "x", "displayName": "X", "fieldTypeName": "Removed.Type", "configuration": { "Whatever": 1 } } ] }
            """);

        _validator.Validate(TableFieldType.ControlName, configuration, configuration).ShouldBeEmpty();
    }

    [Fact]
    public void A_problem_with_the_keys_of_a_composite_type_does_not_hide_the_problems_of_its_inline_fields()
    {
        var configuration = Json("""
            {
              "Table.Rows": 3,
              "Table.Columns": [ { "name": "t", "displayName": "T", "fieldTypeName": "Text", "configuration": { "Text.Rows": 5 } } ]
            }
            """);

        var errors = _validator.Validate(TableFieldType.ControlName, configuration);

        errors.Count.ShouldBe(2);
        errors.ShouldContain(error => error.ErrorMessage!.Contains("'Table.Rows'"));
        errors.ShouldContain(error => error.ErrorMessage!.Contains("'Text.Rows'"));
    }

    [Fact]
    public void A_composite_type_that_declares_no_keys_still_has_its_inline_fields_checked()
    {
        var configuration = Json("""
            { "Test.UndeclaredComposite.Fields": [ { "name": "t", "displayName": "T", "fieldTypeName": "Text", "configuration": { "Text.Rows": 5 } } ] }
            """);

        var error = _validator.Validate(UndeclaredCompositeFieldType.ControlName, configuration).ShouldHaveSingleItem();

        error.ErrorMessage!.ShouldContain("inline field 't' (Text)");
        error.ErrorMessage.ShouldContain("'Text.Rows'");

        // Its own keys are still nobody's business: it declared nothing.
        _validator.Validate(UndeclaredCompositeFieldType.ControlName, Json("""{ "Anything": 1 }""")).ShouldBeEmpty();
    }

    [Fact]
    public void A_configuration_the_composite_type_cannot_read_is_reported_when_nothing_else_has_said_so()
    {
        var unreadable = Json("""{ "Test.UndeclaredComposite.Fields": "x" }""");
        var readable = Json("""{ "Test.UndeclaredComposite.Fields": [] }""");

        _validator.Validate(UndeclaredCompositeFieldType.ControlName, unreadable).ShouldHaveSingleItem()
            .ErrorMessage.ShouldBe("The configuration of the Test.UndeclaredComposite field type is not in a shape the field type can read.");

        // On an update, one that was just as unreadable before is not this edit's doing - but one the edit broke is.
        _validator.Validate(UndeclaredCompositeFieldType.ControlName, unreadable, unreadable).ShouldBeEmpty();
        _validator.Validate(UndeclaredCompositeFieldType.ControlName, unreadable, readable).ShouldHaveSingleItem();
    }

    [Fact]
    public void A_composite_configuration_of_the_wrong_shape_is_reported_instead_of_being_walked()
    {
        _validator.Validate(MatrixFieldType.ControlName, Json("""{ "Matrix.BlockTypes": "x" }""")).ShouldHaveSingleItem();
        _validator.Validate(MatrixFieldType.ControlName, Json("""{ "Matrix.BlockTypes": [ { "name": 5 } ] }""")).ShouldHaveSingleItem();
        _validator.Validate(TableFieldType.ControlName, Json("""{ "Table.Columns": { "name": "x" } }""")).ShouldHaveSingleItem();
    }

    [Fact]
    public void A_configuration_nested_beyond_the_limit_is_not_walked_to_the_bottom()
    {
        // An unvetted client configuration is exactly what this walks, so it cannot be allowed to recurse as
        // deep as the client likes. Refusing such a configuration is the nesting check's job, not this one's:
        // the unknown key nine levels down is simply never reached.
        var configuration = NestInTables(Json("""{ "Text.Rows": 5 }"""), TextFieldType.ControlName, levels: 8);

        _validator.Validate(TableFieldType.ControlName, configuration).ShouldBeEmpty();
    }

    [Fact]
    public void A_configuration_too_deep_to_read_at_all_is_reported_rather_than_thrown()
    {
        // Past the JSON reader's own depth limit the value cannot even be looked at.
        var configuration = NestInTables(new FieldConfigurationDictionary(), TextFieldType.ControlName, levels: 200);

        Should.NotThrow(() => _validator.Validate(TableFieldType.ControlName, configuration));
    }

    private static FieldConfigurationDictionary NestInTables(
        FieldConfigurationDictionary innermost,
        string innermostFieldTypeName,
        int levels)
    {
        var configuration = innermost;
        var fieldTypeName = innermostFieldTypeName;

        for (var level = 0; level < levels; level++)
        {
            var inline = new InlineFieldDefinition
            {
                Name = "f" + level,
                DisplayName = "F",
                FieldTypeName = fieldTypeName,
                Configuration = configuration
            };

            configuration = new FieldConfigurationDictionary();
            _ = new TableConfiguration(configuration) { Columns = new List<InlineFieldDefinition> { inline } };
            fieldTypeName = TableFieldType.ControlName;
        }

        return configuration;
    }

    // ---- the declarations themselves ---------------------------------------------------------------------

    [Fact]
    public void Every_built_in_field_type_declares_its_configuration_keys()
    {
        var builtIns = new[] { "Text", "Number", "DateTime", "Select", "Boolean", "Tree", "Matrix", "Table" };

        foreach (var name in builtIns)
        {
            _resolver.Get(name).ConfigurationKeys.ShouldNotBeNull($"{name} declares nothing");
        }
    }

    [Fact]
    public void Declared_keys_are_exactly_the_keys_the_configuration_classes_name()
    {
        // The declaration restates names that are stored data. A key added to a *ConfigurationNames class
        // without being declared would be refused by the validator; a declared name that is not a constant is
        // a misspelling no reader will ever look up.
        var builtIns = _resolver.GetAll().Where(fieldType => fieldType.GetType().Assembly == typeof(IFieldType).Assembly).ToList();

        builtIns.Count.ShouldBe(8);
        foreach (var fieldType in builtIns)
        {
            AssertDeclaresEveryConfigurationName(fieldType);
        }
    }

    [Fact]
    public void The_ckeditor_field_type_declares_its_configuration_keys()
    {
        var ckEditor = new CKEditorFieldType();

        AssertDeclaresEveryConfigurationName(ckEditor);

        // Its own resolver: the CKEditor module is not (and must not be) part of this project's module graph.
        var validator = new FieldConfigurationValidator(
            new FieldTypeResolver(new IFieldType[] { ckEditor }),
            GetRequiredService<IStringLocalizer<FlexFieldsResource>>());

        validator.Validate(CKEditorFieldType.ControlName,
            Json("""{ "CKEditor.Mode": 0, "CKEditor.ContentFormat": 1, "CKEditor.ImagesContainerName": "images", "CKEditor.InitialContent": "" }"""))
            .ShouldBeEmpty();

        validator.Validate(CKEditorFieldType.ControlName, Json("""{ "CKEditor.Mode": "Full" }""")).ShouldHaveSingleItem();
        validator.Validate(CKEditorFieldType.ControlName, Json("""{ "CKEditor.Toolbar": [] }""")).ShouldHaveSingleItem();
    }

    [Fact]
    public void Every_declared_key_is_unique_and_described()
    {
        foreach (var fieldType in _resolver.GetAll().Where(fieldType => fieldType.ConfigurationKeys != null).Append(new CKEditorFieldType()))
        {
            var keys = fieldType.ConfigurationKeys!;

            keys.Select(key => key.Name).Distinct().Count().ShouldBe(keys.Count, $"{fieldType.Name} declares a key twice");
            keys.ShouldAllBe(key => !string.IsNullOrWhiteSpace(key.Description), $"{fieldType.Name} has an undescribed key");
        }
    }

    [Fact]
    public void A_declared_default_is_a_value_the_key_accepts()
    {
        foreach (var fieldType in _resolver.GetAll().Where(fieldType => fieldType.ConfigurationKeys != null).Append(new CKEditorFieldType()))
        {
            foreach (var key in fieldType.ConfigurationKeys!.Where(key => key.DefaultValue != null))
            {
                var configuration = new FieldConfigurationDictionary { [key.Name] = key.DefaultValue! };

                var validator = fieldType is CKEditorFieldType
                    ? new FieldConfigurationValidator(new FieldTypeResolver(new[] { fieldType }), GetRequiredService<IStringLocalizer<FlexFieldsResource>>())
                    : (IFieldConfigurationValidator)_validator;

                validator.Validate(fieldType.Name, configuration).ShouldBeEmpty($"{key.Name} does not accept its own default");
            }
        }
    }

    [Fact]
    public void A_key_describes_the_shape_of_its_value()
    {
        var text = _resolver.Get(TextFieldType.ControlName).ConfigurationKeys!;
        var select = _resolver.Get(SelectFieldType.ControlName).ConfigurationKeys!;
        var tree = _resolver.Get(TreeFieldType.ControlName).ConfigurationKeys!;
        var table = _resolver.Get(TableFieldType.ControlName).ConfigurationKeys!;

        var mode = text.Single(key => key.Name == "Text.Mode");
        mode.Shape.ShouldBe("integer: 0 (SingleLine) | 1 (MultipleLine)");
        mode.EnumMembers.ShouldBe(new[] { new FieldConfigurationEnumMember(0, "SingleLine"), new FieldConfigurationEnumMember(1, "MultipleLine") });
        mode.DefaultValue.ShouldBe(0);

        text.Single(key => key.Name == "Text.CharLimit").Shape.ShouldBe("integer");

        // The defaults a description names are read from the configuration class, not restated.
        var charLimit = text.Single(key => key.Name == "Text.CharLimit");
        charLimit.Description.ShouldContain("256");
        charLimit.Description.ShouldContain("1024");
        select.Single(key => key.Name == "Select.Options").Shape.ShouldBe("[{ \"text\": string, \"value\": string, \"selected\": boolean }]");

        // A type that contains itself is named the second time, not expanded for ever.
        tree.Single(key => key.Name == "Tree.Nodes").Shape
            .ShouldBe("[{ \"text\": string, \"value\": string, \"selected\": boolean, \"children\": [TreeNodeItem] }]");

        table.Single().Shape.ShouldBe(
            "[{ \"name\": string, \"displayName\": string, \"description\": string, \"fieldTypeName\": string, " +
            "\"required\": boolean, \"configuration\": object }]");
    }

    [Fact]
    public void A_key_is_declared_with_a_value_type_that_fits_its_kind()
    {
        Should.Throw<ArgumentException>(() => new FieldConfigurationKey("A", FieldConfigurationValueKind.Enum));
        Should.Throw<ArgumentException>(() => new FieldConfigurationKey("A", FieldConfigurationValueKind.Enum, typeof(string)));
        Should.Throw<ArgumentException>(() => new FieldConfigurationKey("A", FieldConfigurationValueKind.String, typeof(string)));
        Should.Throw<ArgumentException>(() => new FieldConfigurationKey(" ", FieldConfigurationValueKind.String));

        new FieldConfigurationKey("A", FieldConfigurationValueKind.Array).Shape.ShouldBe("array");
        new FieldConfigurationKey("A", FieldConfigurationValueKind.Object).Shape.ShouldBe("object");
    }

    private void AssertDeclaresEveryConfigurationName(IFieldType fieldType)
    {
        var configurationType = fieldType.GetConfiguration(new FieldConfigurationDictionary()).GetType();
        var namesType = configurationType.Assembly.GetType(configurationType.FullName + "Names");
        namesType.ShouldNotBeNull($"{fieldType.Name}: no {configurationType.Name}Names class");

        var constants = namesType!
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        fieldType.ConfigurationKeys!.Select(key => key.Name).OrderBy(name => name)
            .ShouldBe(constants.OrderBy(name => name), $"{fieldType.Name}: declared keys differ from {namesType.Name}");
    }

    private static FieldConfigurationDictionary Json(string json)
    {
        return JsonSerializer.Deserialize<FieldConfigurationDictionary>(json)!;
    }
}
