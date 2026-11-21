using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Agent.Models;
using NAudio.Wave;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Host-settings section for microphone selection: enumerates NAudio WaveInEvent devices,
    /// persists the chosen device on the shared <see cref="SettingsModel"/>, and shows a live
    /// input-level meter for the selected device.
    /// </summary>
    public partial class MicrophoneSettingsView : UserControl
    {
        private WaveInEvent _waveIn;
        private bool _suppressSelection;

        public MicrophoneSettingsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RefreshDevices();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshDevices();
            StartMonitoring(GetSelectedDeviceNumber());
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopMonitoring();
        }

        private void OnRefreshDevices(object sender, RoutedEventArgs e)
        {
            RefreshDevices();
            RestartMonitoring();
        }

        private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection) return;

            SaveSelectedDevice();
            RestartMonitoring();
        }

        private void RefreshDevices()
        {
            var current = GetSavedDeviceNumber();
            var items = BuildDeviceItems();

            _suppressSelection = true;
            try
            {
                DevicesCombo.ItemsSource = items;
                DevicesCombo.SelectedItem = items.FirstOrDefault(i => i.DeviceNumber == current) ?? items[0];
            }
            finally
            {
                _suppressSelection = false;
            }
        }

        private static List<MicrophoneDeviceItem> BuildDeviceItems()
        {
            var items = new List<MicrophoneDeviceItem>
            {
                new MicrophoneDeviceItem { DeviceNumber = -1, DisplayName = "Default device" }
            };

            try
            {
                var count = WaveInEvent.DeviceCount;
                for (var i = 0; i < count; i++)
                {
                    var caps = WaveInEvent.GetCapabilities(i);
                    items.Add(new MicrophoneDeviceItem { DeviceNumber = i, DisplayName = caps.ProductName });
                }
            }
            catch
            {
                // Best-effort enumeration; keep at least the default entry.
            }

            return items;
        }

        private int GetSavedDeviceNumber()
        {
            return GetSettingsModel()?.MicrophoneDeviceNumber ?? -1;
        }

        private int GetSelectedDeviceNumber()
        {
            return (DevicesCombo.SelectedItem as MicrophoneDeviceItem)?.DeviceNumber ?? -1;
        }

        private void SaveSelectedDevice()
        {
            var settings = GetSettingsModel();
            if (settings != null)
                settings.MicrophoneDeviceNumber = GetSelectedDeviceNumber();
        }

        private SettingsModel GetSettingsModel()
        {
            var dataContext = DataContext;
            if (dataContext == null)
                return null;

            var settingsProperty = dataContext.GetType().GetProperty("Settings");
            return settingsProperty?.GetValue(dataContext) as SettingsModel;
        }

        private void RestartMonitoring()
        {
            StartMonitoring(GetSelectedDeviceNumber());
        }

        private void StartMonitoring(int deviceNumber)
        {
            StopMonitoring();

            WaveInEvent wave = null;
            try
            {
                wave = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 100
                };
                wave.DataAvailable += OnDataAvailable;
                wave.RecordingStopped += OnRecordingStopped;
                wave.StartRecording();
                _waveIn = wave;
            }
            catch
            {
                if (wave != null)
                {
                    try { wave.Dispose(); } catch { /* Best-effort cleanup. */ }
                }
                SetLevel(0);
            }
        }

        private void StopMonitoring()
        {
            var wave = _waveIn;
            _waveIn = null;
            if (wave == null) return;

            try
            {
                wave.DataAvailable -= OnDataAvailable;
                wave.RecordingStopped -= OnRecordingStopped;
                wave.StopRecording();
            }
            catch
            {
                // Device may already be stopped/disposed.
            }

            try
            {
                wave.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            SetLevel(ComputePeak(e.Buffer, e.BytesRecorded) * 100.0);
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
        }

        private void SetLevel(double value)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => SetLevel(value)));
                return;
            }

            LevelBar.Value = value;
        }

        private static double ComputePeak(byte[] buffer, int bytesRecorded)
        {
            if (buffer == null || bytesRecorded < 2)
                return 0.0;

            var max = 0;
            for (var i = 0; i + 1 < bytesRecorded; i += 2)
            {
                var sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                var abs = sample < 0 ? -sample : sample;
                if (abs > max) max = abs;
            }

            return Math.Min(1.0, max / 32768.0);
        }

        private sealed class MicrophoneDeviceItem
        {
            public int DeviceNumber { get; set; }
            public string DisplayName { get; set; }
        }
    }
}
