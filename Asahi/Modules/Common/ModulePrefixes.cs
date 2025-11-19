using Asahi.BotEmoteManagement;

namespace Asahi.Modules;

[GenerateHashedIds]
[HashedId("TestPrefix")]
//
[HashedId("RedButton")]
//
[HashedId("SpoilerModal")]
[HashedId("SpoilerModalContextInput")]
//
[HashedId("BirthdayTextModal")]
public static partial class ModulePrefixes
{
    [GenerateHashedIds]
    [HashedId("AnimeChoiceButtonId")]
    [HashedId("ThemeChoiceButtonId")]
    [HashedId("BackButtonId")]
    [HashedId("RefreshVideoId")]
    public static partial class AnimeThemes
    {
    }

    public static partial class Lookup
    {
        [GenerateHashedIds]
        [HashedId("MediaChoiceButtonId")]
        public static partial class AniList
        {
        }
    }

    [GenerateHashedIds]
    // just making extra sure this stays constant
    [HashedId("MoreInfoButton", UnhashedValue = "Danbooru CV2 more info button")]
    [HashedId("DeleteButton", UnhashedValue = "Danbooru CV2 delete button")]
    [HashedId("DeletionRestoreButton", UnhashedValue = "Danbooru CV2 deletion restore button")]
    //
    [HashedId("DeletionNotesButton", UnhashedValue = "Danbooru CV2 deletion notes button")]
    //
    [HashedId("DeletionNotesModal", UnhashedValue = "Danbooru CV2 deletion notes modal")]
    [HashedId("DeletionNotesModalNoteInput", UnhashedValue = "Danbooru CV2 deletion notes modal - note input")]
    public static partial class Danbooru
    {
    }
}
