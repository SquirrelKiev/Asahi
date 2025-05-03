using Asahi.Modules.Models;
using Refit;

namespace Asahi.Modules;

public interface IMisskeyApi
{
    [Get($"/notes/{{{nameof(noteId)}}}.json")]
    Task<ApiResponse<MisskeyNote>> GetNote(string noteId);
}
