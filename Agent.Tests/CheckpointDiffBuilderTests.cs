using System;
using System.IO;
using NUnit.Framework;
using Agent.Core.Checkpoints;

namespace Agent.Tests
{
    [TestFixture]
    public class CheckpointDiffBuilderTests
    {
        [Test]
        public void Build_Null_ReturnsEmpty()
        {
            Assert.That(CheckpointDiffBuilder.Build(null), Is.Empty);
        }

        [Test]
        public void Build_Empty_ReturnsEmpty()
        {
            Assert.That(CheckpointDiffBuilder.Build(new CheckpointDiff[0]), Is.Empty);
        }

        [Test]
        public void Build_UnchangedFile_ProducesSingleUnchangedRow()
        {
            var files = CheckpointDiffBuilder.Build(new[]
            {
                new CheckpointDiff { RelativePath = "a.txt", Before = "hello", After = "hello" }
            });

            Assert.That(files, Has.Count.EqualTo(1));
            var file = files[0];
            Assert.That(file.RelativePath, Is.EqualTo("a.txt"));
            Assert.That(file.Rows, Has.Count.EqualTo(1));
            Assert.That(file.Rows[0].Kind, Is.EqualTo(DiffRowKind.Unchanged));
            Assert.That(file.AddedCount, Is.EqualTo(0));
            Assert.That(file.RemovedCount, Is.EqualTo(0));
            Assert.That(file.ChangeSummary, Is.EqualTo("+0 -0"));
        }

        [Test]
        public void Build_ModifiedLine_CountsAddAndRemove()
        {
            var files = CheckpointDiffBuilder.Build(new[]
            {
                new CheckpointDiff { RelativePath = "a.txt", Before = "old", After = "new" }
            });

            var file = files[0];
            Assert.That(file.AddedCount, Is.EqualTo(1));
            Assert.That(file.RemovedCount, Is.EqualTo(1));
            Assert.That(file.ChangeSummary, Is.EqualTo("+1 -1"));
        }

        [Test]
        public void Build_InsertedLine_CountsOnlyAdd()
        {
            var files = CheckpointDiffBuilder.Build(new[]
            {
                new CheckpointDiff { Before = "", After = "brand new" }
            });

            var file = files[0];
            Assert.That(file.AddedCount, Is.EqualTo(1));
            Assert.That(file.RemovedCount, Is.EqualTo(0));
            Assert.That(file.ChangeSummary, Is.EqualTo("+1 -0"));
        }

        [Test]
        public void Build_DeletedLine_CountsOnlyRemove()
        {
            var files = CheckpointDiffBuilder.Build(new[]
            {
                new CheckpointDiff { Before = "gone", After = "" }
            });

            var file = files[0];
            Assert.That(file.AddedCount, Is.EqualTo(0));
            Assert.That(file.RemovedCount, Is.EqualTo(1));
            Assert.That(file.ChangeSummary, Is.EqualTo("+0 -1"));
        }

        [Test]
        public void Build_NullRelativePathAndContents_AreTolerated()
        {
            var files = CheckpointDiffBuilder.Build(new[]
            {
                new CheckpointDiff { RelativePath = null, Before = null, After = null }
            });

            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(files[0].RelativePath, Is.EqualTo(""));
            Assert.That(files[0].AddedCount, Is.EqualTo(0));
            Assert.That(files[0].RemovedCount, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class CheckpointExcludePatternsTests
    {
        [Test]
        public void Defaults_StartsWithGitAndContainsCategories()
        {
            var defaults = CheckpointExcludePatterns.Defaults();

            Assert.That(defaults[0], Is.EqualTo(".git/"));
            Assert.That(defaults, Does.Contain("bin/"));
            Assert.That(defaults, Does.Contain("node_modules/"));
            Assert.That(defaults, Does.Contain("*.png"));
            Assert.That(defaults, Does.Contain("*.DS_Store"));
            Assert.That(defaults, Does.Contain("*.zip"));
            Assert.That(defaults, Does.Contain("*.log"));
        }

        [Test]
        public void Build_NullOrEmptyDir_ReturnsDefaults()
        {
            var defaults = CheckpointExcludePatterns.Defaults();
            Assert.That(CheckpointExcludePatterns.Build(null), Is.EqualTo(defaults));
            Assert.That(CheckpointExcludePatterns.Build(""), Is.EqualTo(defaults));
            Assert.That(CheckpointExcludePatterns.Build("   "), Is.EqualTo(defaults));
        }

        [Test]
        public void Build_MissingGitAttributes_ReturnsDefaults()
        {
            var dir = NewTempDir();
            try
            {
                Assert.That(CheckpointExcludePatterns.Build(dir), Is.EqualTo(CheckpointExcludePatterns.Defaults()));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void Build_ReadsLfsPatterns()
        {
            var dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, ".gitattributes"),
                    "*.png filter=lfs diff=lfs merge=lfs -text\n" +
                    "*.bin filter=lfs\n" +
                    "*.txt -text\n");

                var patterns = CheckpointExcludePatterns.Build(dir);

                Assert.That(patterns, Does.Contain("*.png"));
                Assert.That(patterns, Does.Contain("*.bin"));
                Assert.That(patterns, Does.Not.Contain("*.txt"));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void Build_LfsPatternWithTabs_IsExtracted()
        {
            var dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, ".gitattributes"), "*.dll\tfilter=lfs\n");

                var patterns = CheckpointExcludePatterns.Build(dir);

                Assert.That(patterns, Does.Contain("*.dll"));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "agent-checkpoint-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [TestFixture]
    public class CheckpointModelsTests
    {
        [Test]
        public void Checkpoint_DefaultsAndAssignment()
        {
            var checkpoint = new Checkpoint { Ref = "refs/heads/x", Sequence = 2, Message = "snapshot" };
            Assert.That(checkpoint.Ref, Is.EqualTo("refs/heads/x"));
            Assert.That(checkpoint.Sequence, Is.EqualTo(2));
            Assert.That(checkpoint.Message, Is.EqualTo("snapshot"));
        }

        [Test]
        public void CheckpointDiff_Defaults()
        {
            var diff = new CheckpointDiff();
            Assert.That(diff.RelativePath, Is.Null);
            Assert.That(diff.AbsolutePath, Is.Null);
            Assert.That(diff.Before, Is.Null);
            Assert.That(diff.After, Is.Null);
        }

        [Test]
        public void CheckpointSaveResult_Defaults()
        {
            var result = new CheckpointSaveResult();
            Assert.That(result.CommitHash, Is.Null);
            Assert.That(result.IsEmpty, Is.False);
        }

        [Test]
        public void CheckpointServiceOptions_Defaults()
        {
            var options = new CheckpointServiceOptions();
            Assert.That(options.TaskId, Is.Null);
            Assert.That(options.WorkspaceDir, Is.Null);
            Assert.That(options.StateDir, Is.Null);
            Assert.That(options.Log, Is.Null);
        }

        [Test]
        public void InitResult_Defaults()
        {
            var result = new InitResult();
            Assert.That(result.Created, Is.False);
            Assert.That(result.BaseHash, Is.Null);
            Assert.That(result.DurationMs, Is.EqualTo(0));
        }
    }
}
