using System.Runtime.InteropServices;
using Vortice;
using Vortice.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>
/// Проверка наличия зарегистрированных MFT энкодеров под выход H.264 и AAC (для движка — <c>IMFSinkWriter</c> + эти типы; см. <see cref="RecordingOutputFormat"/>).
/// </summary>
public static class MediaFoundationEncoderCatalog
{
    private static readonly EnumFlag s_enumFlags =
        EnumFlag.EnumFlagSyncmft
        | EnumFlag.EnumFlagAsyncmft
        | EnumFlag.EnumFlagHardware
        | EnumFlag.EnumFlagLocalmft;

    /// <summary>
    /// Перечисляет энкодеры с заданным выходным типом (для видео — выход сжатого H.264, для аудио — AAC).
    /// Освобождает объекты активации после подсчёта.
    /// </summary>
    public static int CountEncoders(Guid transformCategory, RegisterTypeInfo outputRegisterType)
    {
        MediaFoundationLifetime.AddRef();
        try
        {
            MediaFactory.MFTEnumEx(
                transformCategory,
                (uint)s_enumFlags,
                inputType: null,
                outputType: outputRegisterType,
                out var ppActivates,
                out var count);

            try
            {
                return (int)count;
            }
            finally
            {
                FreeActivateArray(ppActivates, count);
            }
        }
        finally
        {
            MediaFoundationLifetime.Release();
        }
    }

    /// <summary>Сколько зарегистрировано энкодеров с выходом H.264 (видео).</summary>
    public static int CountH264VideoEncoders()
    {
        var output = new RegisterTypeInfo
        {
            GuidMajorType = MediaTypeGuids.Video,
            GuidSubtype = VideoFormatGuids.FromFourCC(new FourCC("H264")),
        };
        return CountEncoders(TransformCategoryGuids.VideoEncoder, output);
    }

    /// <summary>Сколько зарегистрировано энкодеров с выходом AAC (аудио).</summary>
    public static int CountAacEncoders()
    {
        var output = new RegisterTypeInfo
        {
            GuidMajorType = MediaTypeGuids.Audio,
            GuidSubtype = AudioFormatGuids.Aac,
        };
        return CountEncoders(TransformCategoryGuids.AudioEncoder, output);
    }

    private static void FreeActivateArray(nint ppActivates, uint count)
    {
        if (ppActivates == 0 || count == 0)
            return;

        for (uint i = 0; i < count; i++)
        {
            var pActivate = Marshal.ReadIntPtr(ppActivates, checked((int)(i * nint.Size)));
            if (pActivate == 0)
                continue;
            using var act = new IMFActivate(pActivate);
        }

        Marshal.FreeCoTaskMem(ppActivates);
    }
}
