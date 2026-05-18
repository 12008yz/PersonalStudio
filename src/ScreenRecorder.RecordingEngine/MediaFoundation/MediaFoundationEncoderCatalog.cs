using System.Runtime.InteropServices;
using Vortice;
using Vortice.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>
/// Проверка наличия зарегистрированных MFT энкодеров под выход H.264 и AAC (для движка — <c>IMFSinkWriter</c> + эти типы; см. <see cref="RecordingOutputFormat"/>).
/// </summary>
public static class MediaFoundationEncoderCatalog
{
    private static readonly Guid MftFriendlyNameAttribute = new("314FFBAE-5B41-4C95-9C19-4E7D586EEC7D");

    private static readonly EnumFlag s_enumFlags =
        EnumFlag.EnumFlagSyncmft
        | EnumFlag.EnumFlagAsyncmft
        | EnumFlag.EnumFlagHardware
        | EnumFlag.EnumFlagLocalmft;

    private static readonly EnumFlag s_hardwareEnumFlags =
        EnumFlag.EnumFlagHardware
        | EnumFlag.EnumFlagSyncmft
        | EnumFlag.EnumFlagAsyncmft
        | EnumFlag.EnumFlagLocalmft;

    /// <summary>Сколько зарегистрировано энкодеров с выходом H.264 (уникальные CLSID).</summary>
    public static int CountH264VideoEncoders() => ListH264VideoEncoders().Count;

    /// <summary>Сколько зарегистрировано энкодеров с выходом AAC (уникальные CLSID).</summary>
    public static int CountAacEncoders() => ListAacEncoders().Count;

    public static IReadOnlyList<MediaFoundationEncoderInfo> ListH264VideoEncoders() =>
        ListEncoders(TransformCategoryGuids.VideoEncoder, CreateH264OutputRegisterType(), MediaFoundationEncoderKind.H264Video);

    public static IReadOnlyList<MediaFoundationEncoderInfo> ListAacEncoders() =>
        ListEncoders(TransformCategoryGuids.AudioEncoder, CreateAacOutputRegisterType(), MediaFoundationEncoderKind.AacAudio);

    private static RegisterTypeInfo CreateH264OutputRegisterType() => new()
    {
        GuidMajorType = MediaTypeGuids.Video,
        GuidSubtype = VideoFormatGuids.FromFourCC(new FourCC("H264")),
    };

    private static RegisterTypeInfo CreateAacOutputRegisterType() => new()
    {
        GuidMajorType = MediaTypeGuids.Audio,
        GuidSubtype = AudioFormatGuids.Aac,
    };

    private static IReadOnlyList<MediaFoundationEncoderInfo> ListEncoders(
        Guid transformCategory,
        RegisterTypeInfo outputRegisterType,
        MediaFoundationEncoderKind kind)
    {
        var hardwareClsids = EnumerateEncoders(transformCategory, outputRegisterType, s_hardwareEnumFlags)
            .Select(e => e.TransformClsid)
            .ToHashSet();

        return EnumerateEncoders(transformCategory, outputRegisterType, s_enumFlags)
            .GroupBy(e => e.TransformClsid)
            .Select(g => g.First())
            .Select(e => new MediaFoundationEncoderInfo(
                e.TransformClsid,
                e.FriendlyName,
                IsHardware: hardwareClsids.Contains(e.TransformClsid),
                Kind: kind))
            .OrderByDescending(e => e.IsHardware)
            .ThenBy(e => e.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private readonly record struct EnumeratedEncoder(Guid TransformClsid, string FriendlyName);

    private static List<EnumeratedEncoder> EnumerateEncoders(
        Guid transformCategory,
        RegisterTypeInfo outputRegisterType,
        EnumFlag flags)
    {
        MediaFoundationLifetime.AddRef();
        try
        {
            MediaFactory.MFTEnumEx(
                transformCategory,
                (uint)flags,
                inputType: null,
                outputType: outputRegisterType,
                out var ppActivates,
                out var count);

            try
            {
                var encoders = new List<EnumeratedEncoder>();
                if (ppActivates == 0 || count == 0)
                    return encoders;

                for (uint i = 0; i < count; i++)
                {
                    var pActivate = Marshal.ReadIntPtr(ppActivates, checked((int)(i * nint.Size)));
                    if (pActivate == 0)
                        continue;

                    using var activate = new IMFActivate(pActivate);
                    encoders.Add(ReadEnumeratedEncoder(activate));
                }

                return encoders;
            }
            finally
            {
                if (ppActivates != 0)
                    Marshal.FreeCoTaskMem(ppActivates);
            }
        }
        finally
        {
            MediaFoundationLifetime.Release();
        }
    }

    private static EnumeratedEncoder ReadEnumeratedEncoder(IMFActivate activate)
    {
        var clsid = activate.GetGUID(TransformAttributeKeys.MftTransformClsidAttribute);
        var friendlyName = TryGetFriendlyName(activate, clsid);
        return new EnumeratedEncoder(clsid, friendlyName);
    }

    private static string TryGetFriendlyName(IMFActivate activate, Guid transformClsid)
    {
        try
        {
            var name = activate.GetString(MftFriendlyNameAttribute);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch
        {
            // Fall through to CLSID label.
        }

        return $"MFT {transformClsid:D}";
    }
}
