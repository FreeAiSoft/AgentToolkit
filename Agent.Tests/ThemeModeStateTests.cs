using Agent.Models;
using NUnit.Framework;

namespace Agent.Tests
{
    [TestFixture]
    public class ThemeModeStateTests
    {
        [Test]
        public void DefaultMode_IsManual()
        {
            var state = new ThemeModeState();
            Assert.That(state.Mode, Is.EqualTo(AgentThemeMode.Manual));
        }

        [Test]
        public void DefaultCurrent_IsDark()
        {
            var state = new ThemeModeState();
            Assert.That(state.Current, Is.EqualTo("Dark"));
        }

        [Test]
        public void ManualMode_AcceptsToolkitApply_NotHostApply()
        {
            var state = new ThemeModeState(); // Manual by default
            Assert.That(state.AcceptsToolkitApply, Is.True);
            Assert.That(state.AcceptsHostApply, Is.False);
        }

        [Test]
        public void AutoMode_AcceptsHostApply_NotToolkitApply()
        {
            var state = new ThemeModeState();
            state.SetMode(AgentThemeMode.Auto);
            Assert.That(state.AcceptsHostApply, Is.True);
            Assert.That(state.AcceptsToolkitApply, Is.False);
        }

        [Test]
        public void TryChange_FirstChange_ReturnsTrue_AndUpdatesCurrent()
        {
            var state = new ThemeModeState();
            Assert.That(state.TryChange("Light"), Is.True);
            Assert.That(state.Current, Is.EqualTo("Light"));
        }

        [Test]
        public void TryChange_SameTheme_ReturnsFalse_AndKeepsCurrent()
        {
            var state = new ThemeModeState();
            state.TryChange("Light");
            Assert.That(state.TryChange("Light"), Is.False);
            Assert.That(state.Current, Is.EqualTo("Light"));
        }

        [Test]
        public void TryChange_IsCaseInsensitive()
        {
            var state = new ThemeModeState();
            state.TryChange("Light");
            Assert.That(state.TryChange("LIGHT"), Is.False);
        }

        [Test]
        public void Record_UpdatesCurrent_WithoutChangeFlag()
        {
            var state = new ThemeModeState();
            state.Record("Light");
            Assert.That(state.Current, Is.EqualTo("Light"));
        }
    }
}
