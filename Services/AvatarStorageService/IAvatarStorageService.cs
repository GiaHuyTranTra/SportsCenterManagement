using System.IO;
using System.Threading.Tasks;

namespace Services.AvatarStorageService;

public enum SaveAvatarResult
{
    Success,
    EmptyFile,
    FileTooLarge,
    UnsupportedFormat
}

public interface IAvatarStorageService
{
    /// <summary>
    /// Persists an uploaded avatar for <paramref name="accountId"/> and returns the
    /// public relative URL to store on the profile. The caller passes the raw
    /// stream; the file type is detected from the content, never from the
    /// client-supplied name or content type.
    /// </summary>
    Task<(SaveAvatarResult Result, string? AvatarUrl)> SaveAsync(
        string accountId,
        Stream content,
        long length);

    /// <summary>
    /// Deletes a previously stored avatar. External URLs and unknown paths are
    /// ignored, so this is safe to call with whatever the profile currently holds.
    /// </summary>
    void DeleteIfOwned(string? avatarUrl);

    long MaxFileSizeBytes { get; }
}
