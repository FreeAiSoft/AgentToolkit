using System;
using NUnit.Framework;
using Agent.Core.Contracts;
using Agent.Core.Services;
using Agent.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class ThemeSettingsTests
    {
        [Test]
        public void Normalize_NullBaseTheme_DefaultsToDark()
        {
            var settings = new SettingsData { BaseTheme = null };
            SettingsData.Normalize(settings);
            Assert.That(settings.BaseTheme, Is.EqualTo("Dark"));
        }

        [Test]
        public void Normalize_EmptyBaseTheme_DefaultsToDark()
        {
            var settings = new SettingsData { BaseTheme = "" };
            SettingsData.Normalize(settings);
            Assert.That(settings.BaseTheme, Is.EqualTo("Dark"));
        }

        [Test]
        public void Normalize_InvalidBaseTheme_DefaultsToDark()
        {
            var settings = new SettingsData { BaseTheme = "Solarized" };
            SettingsData.Normalize(settings);
            Assert.That(settings.BaseTheme, Is.EqualTo("Dark"));
        }

        [Test]
        public void Normalize_ValidLightTheme_IsAccepted()
        {
            var settings = new SettingsData { BaseTheme = "Light" };
            SettingsData.Normalize(settings);
            Assert.That(settings.BaseTheme, Is.EqualTo("Light"));
        }

        [Test]
        public void Normalize_ValidTheme_IsCaseInsensitive()
        {
            var settings = new SettingsData { BaseTheme = "light" };
            SettingsData.Normalize(settings);
            Assert.That(string.Equals(settings.BaseTheme, "Light", StringComparison.OrdinalIgnoreCase), Is.True);
        }

        [Test]
        public void SettingsModel_Defaults_ToDarkTheme()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            Assert.That(model.BaseTheme, Is.EqualTo("Dark"));
            Assert.That(model.ThemeIndex, Is.EqualTo(0));
        }

        [Test]
        public void SettingsModel_ThemeIndex_One_SetsBaseThemeToLight()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            model.ThemeIndex = 1;

            Assert.That(model.BaseTheme, Is.EqualTo("Light"));
        }

        [Test]
        public void SettingsModel_BaseTheme_Light_SetsThemeIndexToOne()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            model.BaseTheme = "Light";

            Assert.That(model.ThemeIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsModel_BaseTheme_IsCaseInsensitive()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData { BaseTheme = "light" }));
            model.Load();

            Assert.That(model.ThemeIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsModel_ThemeIndex_RoundTrips()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            model.ThemeIndex = 1;
            Assert.That(model.ThemeIndex, Is.EqualTo(1));
            Assert.That(model.BaseTheme, Is.EqualTo("Light"));

            model.ThemeIndex = 0;
            Assert.That(model.ThemeIndex, Is.EqualTo(0));
            Assert.That(model.BaseTheme, Is.EqualTo("Dark"));
        }

        [Test]
        public void Normalize_InvalidThemeMode_DefaultsToManual()
        {
            var settings = new SettingsData { ThemeMode = "Solarized" };
            SettingsData.Normalize(settings);
            Assert.That(settings.ThemeMode, Is.EqualTo("Manual"));
        }

        [Test]
        public void Normalize_EmptyThemeMode_DefaultsToManual()
        {
            var settings = new SettingsData { ThemeMode = "" };
            SettingsData.Normalize(settings);
            Assert.That(settings.ThemeMode, Is.EqualTo("Manual"));
        }

        [Test]
        public void Normalize_ValidAutoThemeMode_IsAccepted()
        {
            var settings = new SettingsData { ThemeMode = "Auto" };
            SettingsData.Normalize(settings);
            Assert.That(settings.ThemeMode, Is.EqualTo("Auto"));
        }

        [Test]
        public void Normalize_ThemeMode_IsCaseInsensitive()
        {
            var settings = new SettingsData { ThemeMode = "auto" };
            SettingsData.Normalize(settings);
            Assert.That(string.Equals(settings.ThemeMode, "Auto", StringComparison.OrdinalIgnoreCase), Is.True);
        }

        [Test]
        public void SettingsModel_Defaults_ToManualThemeMode()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            Assert.That(model.ThemeMode, Is.EqualTo("Manual"));
            Assert.That(model.ThemeModeIndex, Is.EqualTo(0));
        }

        [Test]
        public void SettingsModel_ThemeModeIndex_One_SetsThemeModeToAuto()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            model.ThemeModeIndex = 1;

            Assert.That(model.ThemeMode, Is.EqualTo("Auto"));
        }

        [Test]
        public void SettingsModel_ThemeMode_Auto_SetsThemeModeIndexToOne()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            model.ThemeMode = "Auto";

            Assert.That(model.ThemeModeIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsModel_ThemeMode_IsCaseInsensitive()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData { ThemeMode = "auto" }));
            model.Load();

            Assert.That(model.ThemeModeIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsModel_ThemeModeIndex_RoundTrips()
        {
            var model = new SettingsModel(new FakeSettingsStore(new SettingsData()));
            model.Load();

            model.ThemeModeIndex = 1;
            Assert.That(model.ThemeModeIndex, Is.EqualTo(1));
            Assert.That(model.ThemeMode, Is.EqualTo("Auto"));

            model.ThemeModeIndex = 0;
            Assert.That(model.ThemeModeIndex, Is.EqualTo(0));
            Assert.That(model.ThemeMode, Is.EqualTo("Manual"));
        }

        private sealed class FakeSettingsStore : ISettingsStore
        {
            private readonly SettingsData _data;

            public FakeSettingsStore(SettingsData data) => _data = data ?? new SettingsData();

            public SettingsData Load() => _data;

            public void Save(SettingsData settings)
            {
            }
        }
    }
}
