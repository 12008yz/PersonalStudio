namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>GUID из <c>codecapi.h</c> для настройки H.264 MFT через <see cref="IMFSinkWriter.SetInputMediaType"/>.</summary>
internal static class H264CodecApiGuids
{
    public static readonly Guid AVEncMPVGOPSize = new("95f31b26-95a4-41aa-9303-246a7fc6eef1");

    public static readonly Guid AVEncCommonRateControlMode = new("1c0608e9-370c-4710-8a58-cb6181c42423");

    public static readonly Guid AVEncCommonMeanBitRate = new("f7222374-2144-4815-b550-a37f8e12ee52");

    public static readonly Guid AVEncCommonMaxBitRate = new("9651eae4-39b9-4ebf-85ef-d7f444ec7465");
}
