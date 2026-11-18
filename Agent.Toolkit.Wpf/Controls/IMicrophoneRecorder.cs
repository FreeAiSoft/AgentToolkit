using System;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Minimal microphone recorder seam used by <see cref="ChatControl"/> for voice input. The
    /// WPF host provides the concrete implementation (e.g. an NAudio-based recorder); the toolkit
    /// stays free of audio libraries.
    /// </summary>
    public interface IMicrophoneRecorder
    {
        /// <summary>Starts capturing. Throws when the microphone is unavailable.</summary>
        void Start();

        /// <summary>Stops capturing and returns the recorded PCM audio (16 kHz, 16-bit, mono).</summary>
        byte[] Stop();

        /// <summary>Raised while capturing with the current input level (0..1).</summary>
        event EventHandler<double> LevelChanged;
    }
}
