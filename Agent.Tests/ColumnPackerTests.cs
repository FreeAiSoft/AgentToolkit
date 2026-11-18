using System;
using NUnit.Framework;
using Agent.Core.Layout;

namespace Agent.Tests
{
    [TestFixture]
    public class ColumnPackerTests
    {
        [Test]
        public void Pack_MaxTwoPerColumn_SplitsIntoPairs()
        {
            var columns = ColumnPacker.Pack(
                new double[] { 10, 10, 10, 10 },
                spacing: 4,
                availableHeight: double.PositiveInfinity,
                maxItemsPerColumn: 2);

            Assert.That(columns, Has.Count.EqualTo(2));
            Assert.That(columns[0], Is.EqualTo(new[] { 3, 2 }));
            Assert.That(columns[1], Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void Pack_NoLimit_UsesSingleColumn()
        {
            var columns = ColumnPacker.Pack(
                new double[] { 10, 10, 10, 10 },
                spacing: 4,
                availableHeight: double.PositiveInfinity,
                maxItemsPerColumn: 0);

            Assert.That(columns, Has.Count.EqualTo(1));
            Assert.That(columns[0], Is.EqualTo(new[] { 3, 2, 1, 0 }));
        }

        [Test]
        public void Pack_HeightOverflow_BreaksColumn()
        {
            var columns = ColumnPacker.Pack(
                new double[] { 10, 10, 10 },
                spacing: 4,
                availableHeight: 30,
                maxItemsPerColumn: 0);

            Assert.That(columns, Has.Count.EqualTo(2));
            Assert.That(columns[0], Is.EqualTo(new[] { 2, 1 }));
            Assert.That(columns[1], Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void Pack_LimitCombinedWithHeightOverflow()
        {
            var columns = ColumnPacker.Pack(
                new double[] { 10, 10, 10, 10, 10 },
                spacing: 4,
                availableHeight: 24,
                maxItemsPerColumn: 2);

            Assert.That(columns, Has.Count.EqualTo(3));
            Assert.That(columns[0], Is.EqualTo(new[] { 4, 3 }));
            Assert.That(columns[1], Is.EqualTo(new[] { 2, 1 }));
            Assert.That(columns[2], Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void Pack_EmptyInput_ReturnsNoColumns()
        {
            var columns = ColumnPacker.Pack(
                Array.Empty<double>(),
                spacing: 4,
                availableHeight: double.PositiveInfinity,
                maxItemsPerColumn: 2);

            Assert.That(columns, Is.Empty);
        }
    }
}
