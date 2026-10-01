using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace Services.AvatarStorageService;

public class AvatarStorageService : IAvatarStorageService
{
    // Public path segment; also the folder name under the web root.
    private const string AvatarFolder = "uploads/avatars";
    private const long MaxBytes = 2 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public AvatarStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public long MaxFileSizeBytes => MaxBytes;

    public async Task<(SaveAvatarResult Result, string? AvatarUrl)> SaveAsync(
        string accountId,
        Stream content,
        long length)
    {
        if (length <= 0)
        {
            return (SaveAvatarResult.EmptyFile, null);
        }

        if (length > MaxBytes)
        {
            return (SaveAvatarResult.FileTooLarge, null);
        }

        // Buffer the upload so the signature can be inspected before anything is
        // written to disk.
        using MemoryStream buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        byte[] bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            return (SaveAvatarResult.EmptyFile, null);
        }

        if (bytes.Length > MaxBytes)
        {
            return (SaveAvatarResult.FileTooLarge, null);
        }

        string? extension = DetectImageExtension(bytes);
        if (extension is null)
        {
            return (SaveAvatarResult.UnsupportedFormat, null);
        }

        string root = ResolveWebRoot();
        string directory = Path.Combine(root, "uploads", "avatars");
        Directory.CreateDirectory(directory);

        // Remove older avatars for this account so replacing a picture does not
        // leave orphaned files behind.
        string prefix = BuildFilePrefix(accountId);
        foreach (string stale in Directory.EnumerateFiles(directory, prefix + "*"))
        {
            TryDelete(stale);
        }

        // The random suffix makes the URL change on every upload, which keeps
        // browsers and the already-loaded page from showing a cached old image.
        string fileName = $"{prefix}{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), bytes);

        return (SaveAvatarResult.Success, $"/{AvatarFolder}/{fileName}");
    }

    public void DeleteIfOwned(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return;
        }

        string trimmed = avatarUrl.Trim();
        string expectedPrefix = "/" + AvatarFolder + "/";
        if (!trimmed.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            // A preset or externally hosted picture: nothing of ours to remove.
            return;
        }

        string fileName = trimmed[expectedPrefix.Length..];
        // Reject anything that is not a plain file name so a crafted profile value
        // can never reach outside the avatar folder.
        if (fileName.Length == 0
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Contains(".."))
        {
            return;
        }

        TryDelete(Path.Combine(ResolveWebRoot(), "uploads", "avatars", fileName));
    }

    private string ResolveWebRoot()
    {
        string? root = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            // WebRootPath is null until wwwroot exists on disk.
            root = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        Directory.CreateDirectory(root);
        return root;
    }

    // Account ids are GUIDs or similar, but they reach the file system here, so
    // keep only characters that are unambiguously safe in a file name.
    private static string BuildFilePrefix(string accountId)
    {
        string safe = new string(accountId
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            .Take(64)
            .ToArray());

        return (safe.Length > 0 ? safe : "account") + "-";
    }

    /// Detects the format from the file signature. The browser-supplied content
    /// type and file name are attacker-controlled and are deliberately ignored.
    private static string? DetectImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ".png";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return ".webp";
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A locked file must not fail the upload; it is only leftover bytes.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
