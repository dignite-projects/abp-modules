using System;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// <see cref="FlexFieldDictionaryExtensions.RenameField"/> - the primitive every value-bag key migration is
/// built on. Pure dictionary work, so no host, no provider and no database: keeping these here in the
/// Abstractions-only project is what proves renaming needs none of them.
/// </summary>
public class FlexFieldDictionaryRename_Tests
{
    [Fact]
    public void Rename_moves_the_value_to_the_new_key()
    {
        var host = new Host();
        host.SetField("AuthorName", "Ada");

        host.RenameField("AuthorName", "Author").ShouldBeTrue();

        host.HasField("AuthorName").ShouldBeFalse();
        host.GetField("Author").ShouldBe("Ada");
    }

    [Fact]
    public void Rename_leaves_the_other_keys_alone()
    {
        var host = new Host();
        host.SetField("AuthorName", "Ada");
        host.SetField("ViewCount", 42);

        host.RenameField("AuthorName", "Author");

        host.GetField("ViewCount").ShouldBe(42);
        host.FlexFields.Count.ShouldBe(2);
    }

    [Fact]
    public void Renaming_a_key_that_is_not_there_changes_nothing()
    {
        // The basis of a re-runnable migration: entities already migrated - or that never held the field -
        // are a no-op rather than an error.
        var host = new Host();
        host.SetField("ViewCount", 42);

        host.RenameField("AuthorName", "Author").ShouldBeFalse();

        host.HasField("Author").ShouldBeFalse();
        host.FlexFields.Count.ShouldBe(1);
    }

    [Fact]
    public void Renaming_a_key_to_itself_changes_nothing()
    {
        var host = new Host();
        host.SetField("Author", "Ada");

        host.RenameField("Author", "Author").ShouldBeFalse();

        host.GetField("Author").ShouldBe("Ada");
    }

    [Fact]
    public void A_case_only_rename_really_moves_the_key()
    {
        // FlexFieldDictionary inherits Dictionary<string, object>'s ordinal comparer, so these are two
        // distinct keys - treating this as a no-op would silently orphan the value.
        var host = new Host();
        host.SetField("authorName", "Ada");

        host.RenameField("authorName", "AuthorName").ShouldBeTrue();

        host.HasField("authorName").ShouldBeFalse();
        host.GetField("AuthorName").ShouldBe("Ada");
    }

    [Fact]
    public void Renaming_onto_an_occupied_key_throws_rather_than_picking_a_winner()
    {
        var host = new Host();
        host.SetField("AuthorName", "Ada");
        host.SetField("Author", "Grace");

        Should.Throw<InvalidOperationException>(() => host.RenameField("AuthorName", "Author"));

        // Nothing was lost on the way out.
        host.GetField("AuthorName").ShouldBe("Ada");
        host.GetField("Author").ShouldBe("Grace");
    }

    [Fact]
    public void An_occupied_target_is_fine_when_the_source_is_absent()
    {
        // Re-running a migration that already completed: the target holds the migrated value and the source
        // is long gone. That must not look like a collision.
        var host = new Host();
        host.SetField("Author", "Ada");

        host.RenameField("AuthorName", "Author").ShouldBeFalse();

        host.GetField("Author").ShouldBe("Ada");
    }

    private sealed class Host : IHasFlexFields
    {
        public FlexFieldDictionary FlexFields { get; } = new();
    }
}
