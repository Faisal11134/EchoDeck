using System.Windows;
using EchoDeck.App.Models;
using EchoDeck.App.Services;

namespace EchoDeck.App.Views;

public partial class FirstRunDialog : Window
{
    private readonly SettingsService _settingsService;
    private readonly IVoicemeeterService _voicemeeterService;
    private readonly bool _voicemeeterDetected;

    public FirstRunDialog(SettingsService settingsService, IVoicemeeterService voicemeeterService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _voicemeeterService = voicemeeterService;
        _voicemeeterDetected = voicemeeterService.State == VoicemeeterState.Detected;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = _voicemeeterDetected
            ? "Voicemeeter detected. EchoDeck will route all audio through Voicemeeter."
            : "Voicemeeter not detected. EchoDeck requires Voicemeeter to be installed and running.";

        if (_voicemeeterDetected)
        {
            var preferred = _voicemeeterService.GetPreferredOutput(_settingsService.Current);
            var candidate = preferred ?? _voicemeeterService.AvailableVoicemeeterOutputs.FirstOrDefault();
            if (candidate is not null)
            {
                _settingsService.Current.PreferredVoicemeeterOutputDeviceId = candidate.Id;
                DeviceInfo.Text = $"Preferred output: {candidate.Name}";
            }
            else
            {
                DeviceInfo.Text = "No Voicemeeter outputs found.";
            }
        }
        else
        {
            DeviceInfo.Text = "Install Voicemeeter (Standard, Banana, or Potato) from vb-audio.com";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
