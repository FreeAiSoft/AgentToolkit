using System;
using NUnit.Framework;
using Agent.Core.Services;

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
    }
}
