namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Минимальная проверка контейнера MP4 без внешних утилит (ftyp + размер).</summary>
public static class Mp4ContainerValidation
{
    private const int MinimumPlayableSizeBytes = 8 * 1024;

    public static bool TryValidatePlayableFile(string path, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path is empty.";
            return false;
        }

        if (!File.Exists(path))
        {
            errorMessage = "File does not exist.";
            return false;
        }

        var info = new FileInfo(path);
        if (info.Length < MinimumPlayableSizeBytes)
        {
            errorMessage = $"File is too small ({info.Length} bytes).";
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            if (stream.Read(header) < 8)
            {
                errorMessage = "Could not read MP4 header.";
                return false;
            }

            if (!HasFtypBox(header))
            {
                errorMessage = "Missing 'ftyp' box — not a recognizable MP4/MOV container.";
                return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        return true;
    }

    internal static bool HasFtypBox(ReadOnlySpan<byte> atLeastEightBytes)
    {
        if (atLeastEightBytes.Length < 8)
            return false;

        return atLeastEightBytes[4] == (byte)'f'
            && atLeastEightBytes[5] == (byte)'t'
            && atLeastEightBytes[6] == (byte)'y'
            && atLeastEightBytes[7] == (byte)'p';
    }
}
