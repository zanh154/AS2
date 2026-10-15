using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AssignmentPRN.DataAccess.Services;

public sealed record StoredVoiceAudio(Stream Content, string ContentType);
public interface IVoiceAudioStore
{
    Task<string> SaveAsync(int candidateId, int questionId, string contentType, Stream content, CancellationToken ct);
    Task<StoredVoiceAudio> OpenAsync(string key, CancellationToken ct);
}

/// <summary>Immutable, content-addressed recordings outside the public static-file tree.</summary>
public sealed class LocalVoiceAudioStore(string root) : IVoiceAudioStore
{
    public const long MaxBytes = 12 * 1024 * 1024;
    private static readonly Regex KeyPattern = new(@"\A[1-9][0-9]*/[1-9][0-9]*/[a-f0-9]{64}(?:-[a-f0-9]{32})?\.(webm|ogg|wav|m4a)\z", RegexOptions.CultureInvariant);

    public async Task<string> SaveAsync(int candidateId, int questionId, string contentType, Stream content, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(questionId);
        var mime = contentType.Split(';')[0].Trim().ToLowerInvariant();
        var extension = mime switch { "audio/webm" => "webm", "audio/ogg" => "ogg",
            "audio/wav" or "audio/wave" or "audio/x-wav" => "wav", "audio/mp4" => "m4a",
            _ => throw new ArgumentException("Định dạng âm thanh không được hỗ trợ.") };
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes) throw new ArgumentException("Âm thanh vượt giới hạn 12 MB.");
            buffer.Write(chunk, 0, read);
        }
        var bytes = buffer.ToArray();
        if (bytes.Length < 12 || !ValidHeader(bytes, extension))
            throw new ArgumentException("Nội dung tệp không khớp định dạng âm thanh.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var key = $"{candidateId}/{questionId}/{hash}.{extension}";
        var path = Path.Combine(Path.GetFullPath(root), key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Retain any interrupted file and create a fresh immutable copy. Never delete
        // or overwrite it. A retry can reuse an already verified complete copy.
        foreach (var existingPath in Directory.EnumerateFiles(Path.GetDirectoryName(path)!, hash + "*." + extension))
        {
            try {
                var existing = await File.ReadAllBytesAsync(existingPath, ct);
                if (SHA256.HashData(existing).AsSpan().SequenceEqual(SHA256.HashData(bytes)))
                    return $"{candidateId}/{questionId}/{Path.GetFileName(existingPath)}";
            }
            catch (IOException) { /* An in-flight or interrupted copy is retained. */ }
        }
        if (File.Exists(path))
        {
            key = $"{candidateId}/{questionId}/{hash}-{Guid.NewGuid():N}.{extension}";
            path = Path.Combine(Path.GetFullPath(root), key.Replace('/', Path.DirectorySeparatorChar));
        }
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await file.WriteAsync(bytes, ct);
        }
        catch (IOException) when (File.Exists(path))
        {
            var existing = await File.ReadAllBytesAsync(path, ct);
            if (!SHA256.HashData(existing).AsSpan().SequenceEqual(SHA256.HashData(bytes)))
                throw new IOException("Bản ghi trước chưa lưu hoàn tất; hãy thử lưu lại bản thu.");
        }
        return key;
    }

    private static bool ValidHeader(byte[] bytes, string extension) => extension switch {
        "webm" => bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 })
            && Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096)).Contains("webm", StringComparison.Ordinal),
        "ogg" => Encoding.ASCII.GetString(bytes, 0, 4) == "OggS",
        "wav" => Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WAVE",
        "m4a" => Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp",
        _ => false
    };

    public Task<StoredVoiceAudio> OpenAsync(string key, CancellationToken ct)
    {
        if (!KeyPattern.IsMatch(key)) throw new ArgumentException("Đường dẫn âm thanh không hợp lệ.");
        ct.ThrowIfCancellationRequested();
        var type = Path.GetExtension(key) switch { ".webm" => "audio/webm", ".ogg" => "audio/ogg", ".wav" => "audio/wav", _ => "audio/mp4" };
        var path = Path.Combine(Path.GetFullPath(root), key.Replace('/', Path.DirectorySeparatorChar));
        return Task.FromResult(new StoredVoiceAudio(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), type));
    }
}
