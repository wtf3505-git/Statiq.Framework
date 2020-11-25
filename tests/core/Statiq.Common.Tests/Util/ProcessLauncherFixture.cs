﻿using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shouldly;
using Statiq.Testing;

namespace Statiq.Common.Tests.Util
{
    [TestFixture]
    public class ProcessLauncherFixture : BaseFixture
    {
        public class StartNewTests : ProcessLauncherFixture
        {
            [Test]
            public void ThrowsForSynchronousError()
            {
                // Given
                ProcessLauncher processLauncher = new ProcessLauncher("dotnet", "run foobar");
                StringWriter outputWriter = new StringWriter();
                StringWriter errorWriter = new StringWriter();

                // When, Then
                Should.Throw<Exception>(() => processLauncher.StartNew(outputWriter, errorWriter));
                errorWriter.ToString().ShouldContain("Couldn't find a project to run.");
            }

            [Test]
            public void DoesNotThrowForBackgroundError()
            {
                // Given
                ProcessLauncher processLauncher = new ProcessLauncher("dotnet", "run foobar")
                {
                    IsBackground = true
                };
                StringWriter outputWriter = new StringWriter();
                StringWriter errorWriter = new StringWriter();

                // When
                int exitCode = processLauncher.StartNew(outputWriter, errorWriter);

                // Then
                exitCode.ShouldBe(0);
                int count = 0;
                while (processLauncher.AreAnyRunning)
                {
                    Thread.Sleep(1000);
                    count++;
                    if (count > 10)
                    {
                        throw new Exception("Process never returned: " + outputWriter.ToString());
                    }
                }
                errorWriter.ToString().ShouldContain("Couldn't find a project to run.");
            }

            [Test]
            public void LaunchesProcess()
            {
                // Given
                NormalizedPath projectPath = new NormalizedPath(typeof(ProcessLauncherFixture).Assembly.Location)
                    .Parent.Parent.Parent.Parent.Parent.Combine("TestConsoleApp");
                ProcessLauncher processLauncher = new ProcessLauncher("dotnet", $"run --project \"{projectPath.FullPath}\"");
                StringWriter outputWriter = new StringWriter();
                StringWriter errorWriter = new StringWriter();

                // When
                int exitCode = processLauncher.StartNew(outputWriter, errorWriter);

                // Then
                exitCode.ShouldBe(0);
                outputWriter.ToString().ShouldContain("Finished");
            }

            [Test]
            public void ReturnsExitCode()
            {
                // Given
                NormalizedPath projectPath = new NormalizedPath(typeof(ProcessLauncherFixture).Assembly.Location)
                    .Parent.Parent.Parent.Parent.Parent.Combine("TestConsoleApp");
                ProcessLauncher processLauncher = new ProcessLauncher("dotnet", $"run --project \"{projectPath.FullPath}\" -- 0 123")
                {
                    IsErrorExitCode = _ => false
                };
                StringWriter outputWriter = new StringWriter();
                StringWriter errorWriter = new StringWriter();

                // When
                int exitCode = processLauncher.StartNew(outputWriter, errorWriter);

                // Then
                exitCode.ShouldBe(123);
                outputWriter.ToString().ShouldContain("Finished");
            }

            [Test]
            public void SupportsCancellation()
            {
                // Given
                NormalizedPath projectPath = new NormalizedPath(typeof(ProcessLauncherFixture).Assembly.Location)
                    .Parent.Parent.Parent.Parent.Parent.Combine("TestConsoleApp");
                ProcessLauncher processLauncher = new ProcessLauncher("dotnet", $"run --project \"{projectPath.FullPath}\" -- 10");
                StringWriter outputWriter = new StringWriter();
                StringWriter errorWriter = new StringWriter();
                CancellationTokenSource cts = new CancellationTokenSource(5000);

                // When
                Should.Throw<Exception>(() => processLauncher.StartNew(outputWriter, errorWriter, cts.Token));

                // Then
                outputWriter.ToString().ShouldNotContain("Finished");
                processLauncher.AreAnyRunning.ShouldBeFalse();
            }

            [Test]
            public void SupportsBackgroundCancellation()
            {
                // Given
                NormalizedPath projectPath = new NormalizedPath(typeof(ProcessLauncherFixture).Assembly.Location)
                    .Parent.Parent.Parent.Parent.Parent.Combine("TestConsoleApp");
                ProcessLauncher processLauncher = new ProcessLauncher("dotnet", $"run --project \"{projectPath.FullPath}\" -- 10")
                {
                    IsBackground = true
                };
                StringWriter outputWriter = new StringWriter();
                StringWriter errorWriter = new StringWriter();
                CancellationTokenSource cts = new CancellationTokenSource(5000);

                // When
                int exitCode = processLauncher.StartNew(outputWriter, errorWriter, cts.Token);

                // Then
                exitCode.ShouldBe(0);
                int count = 0;
                while (processLauncher.AreAnyRunning)
                {
                    Thread.Sleep(1000);
                    count++;
                    if (count > 10)
                    {
                        throw new Exception("Process never returned: " + outputWriter.ToString());
                    }
                }
                outputWriter.ToString().ShouldNotContain("Finished");
                processLauncher.AreAnyRunning.ShouldBeFalse();
            }
        }
    }
}
