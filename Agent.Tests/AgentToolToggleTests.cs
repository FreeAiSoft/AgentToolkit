using NUnit.Framework;
using Agent.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class AgentToolToggleItemTests
    {
        [Test]
        public void Constructor_AssignsProperties()
        {
            var item = new AgentToolToggleItem("read_spec", "Spec", "Read spec", mutates: false, isEnabled: true);

            Assert.That(item.Name, Is.EqualTo("read_spec"));
            Assert.That(item.Group, Is.EqualTo("Spec"));
            Assert.That(item.Description, Is.EqualTo("Read spec"));
            Assert.That(item.Mutates, Is.False);
            Assert.That(item.IsEnabled, Is.True);
        }

        [Test]
        public void IsEnabled_RaisesPropertyChanged()
        {
            var item = new AgentToolToggleItem("read_spec", "Spec", "Read spec", false, true);
            string changed = null;
            item.PropertyChanged += (s, e) => changed = e.PropertyName;

            item.IsEnabled = false;

            Assert.That(changed, Is.EqualTo(nameof(AgentToolToggleItem.IsEnabled)));
            Assert.That(item.IsEnabled, Is.False);
        }

        [Test]
        public void IsEnabled_SameValue_DoesNotRaise()
        {
            var item = new AgentToolToggleItem("read_spec", "Spec", "Read spec", false, true);
            var raised = 0;
            item.PropertyChanged += (s, e) => raised++;

            item.IsEnabled = true;

            Assert.That(raised, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class AgentToolToggleGroupTests
    {
        [Test]
        public void Constructor_CreatesEmptyItems()
        {
            var group = new AgentToolToggleGroup("RPA");

            Assert.That(group.Name, Is.EqualTo("RPA"));
            Assert.That(group.Items, Is.Not.Null);
            Assert.That(group.Items, Is.Empty);
        }

        [Test]
        public void Items_AreMutableCollection()
        {
            var group = new AgentToolToggleGroup("RPA");
            group.Items.Add(new AgentToolToggleItem("x", "RPA", "d", false, true));

            Assert.That(group.Items, Has.Count.EqualTo(1));
        }
    }
}
