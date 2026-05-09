namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Нефункциональные требования v1: производительность на эталонном железе, A/V-синхронизация, поведение при сбоях.
/// Приёмка по числам — в фазах интеграции и стабильности плана; здесь зафиксированы целевые ориентиры.
/// </summary>
/// <remarks>
/// Продукт захватывает весь монитор (<see cref="RecordingSourcesSpec.MvpFullMonitorOnly"/>). Числа ниже относятся к референсному сценарию
/// до <see cref="ReferenceMaxWidth"/>×<see cref="ReferenceMaxHeight"/> @ <see cref="ReferenceFramesPerSecond"/> fps; при 1440p/4K нагрузка выше — отдельные цели возможны позже.
/// </remarks>
public static class RecordingNfrSpec
{
    /// <summary>Длина скользящего окна для метрики доли дропов кадров (секунды).</summary>
    public const int FrameDropMetricRollingWindowSeconds = 60;
    /// <summary>Эталонная нагрузка для формулировки «типичный ноутбук»: выходное или захватываемое видео до 1080p.</summary>
    public const int ReferenceMaxWidth = 1920;

    /// <summary>Эталонная нагрузка: до 1080p по вертикали.</summary>
    public const int ReferenceMaxHeight = 1080;

    /// <summary>Целевая частота кадров на эталонном сценарии (запись монитора 1080p на типичном ноутбуке).</summary>
    public const int ReferenceFramesPerSecond = 30;

    /// <summary>
    /// На эталонном сценарии не допускается «постоянный» срыв потока кадров: доля пропущенных кадров
    /// (относительно номинального FPS) на любом непрерывном отрезке <see cref="FrameDropMetricRollingWindowSeconds"/> с не выше этого значения.
    /// Уточняется метриками в UI/логах при реализации пайплайна.
    /// </summary>
    public const double MaxFrameDropRatioPerRolling60Seconds = 0.05;

    /// <summary>
    /// Допустимый модуль рассинхрона аудио/видео на контрольном клипе после сведения часов (измерение в фазе интеграции).
    /// Не юридическая гарантия; ориентир для тестов и регрессии.
    /// </summary>
    public const int AcceptableAvDriftMilliseconds = 100;

    /// <summary>
    /// При ошибках (устройство, кодек, нехватка места, отказ MF): показывать пользователю понятное сообщение,
    /// по возможности останавливать запись контролируемо и финализировать контейнер; не маскировать сбой «тихим» обрывом без индикации.
    /// </summary>
    public const bool SurfaceFailuresToUser = true;
}
