using NUnit.Framework;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class AgentModeNamesTests
    {
        [Test]
        public void ToValue_ReturnsDisplayName()
        {
            Assert.That(AgentModeNames.ToValue(AgentMode.Plan), Is.EqualTo("Plan"));
            Assert.That(AgentModeNames.ToValue(AgentMode.Act), Is.EqualTo("Act"));
            Assert.That(AgentModeNames.ToValue(AgentMode.Help), Is.EqualTo("Help"));
        }

        [Test]
        public void ToValue_UnknownValue_FallsBackToPlan()
        {
            Assert.That(AgentModeNames.ToValue((AgentMode)999), Is.EqualTo("Plan"));
        }

        [TestCase("Act", AgentMode.Act)]
        [TestCase("act", AgentMode.Act)]
        [TestCase("Help", AgentMode.Help)]
        [TestCase("plan", AgentMode.Plan)]
        [TestCase(null, AgentMode.Plan)]
        [TestCase("", AgentMode.Plan)]
        [TestCase("unknown", AgentMode.Plan)]
        public void Parse_ReturnsExpectedMode(object value, AgentMode expected)
        {
            Assert.That(AgentModeNames.Parse(value), Is.EqualTo(expected));
        }

        [Test]
        public void Parse_NonStringValue_FallsBackToPlan()
        {
            Assert.That(AgentModeNames.Parse(123), Is.EqualTo(AgentMode.Plan));
        }
    }
}
