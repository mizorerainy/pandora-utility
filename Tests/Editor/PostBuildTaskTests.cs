using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using MizoreRainy.Pandora.BuildUtility;
using UnityEngine.TestTools;

namespace MizoreRainy.Pandora.Tests.Editor
{
    public class PostBuildTaskTests
    {
        private BuildSettingsUtility _utility;

        [SetUp]
        public void Setup()
        {
            _utility = ScriptableObject.CreateInstance<BuildSettingsUtility>();
        }

        // Mock class to track execution
        private class MockTask : ManagedPostBuildTask
        {
            public bool WasExecuted { get; private set; }
            public bool ThrowException { get; set; }
            
            public override void Execute(ManagedBuildProfile profile, string buildOutputPath)
            {
                if (ThrowException) throw new Exception("Test Exception");
                WasExecuted = true;
            }
        }

        [Test]
        public void ExecutePostBuildTasks_RunsEnabledTasksInOrder()
        {
            MethodInfo method = typeof(BuildSettingsUtility).GetMethod("ExecutePostBuildTasks", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "ExecutePostBuildTasks method not found.");

            var profile = new ManagedBuildProfile();
            var task1 = new MockTask { IsEnabled = true };
            var task2 = new MockTask { IsEnabled = true };
            
            profile.PostBuildTasks.Add(task1);
            profile.PostBuildTasks.Add(task2);

            method.Invoke(_utility, new object[] { profile, "test/path" });

            Assert.IsTrue(task1.WasExecuted, "Task 1 should have been executed.");
            Assert.IsTrue(task2.WasExecuted, "Task 2 should have been executed.");
        }

        [Test]
        public void ExecutePostBuildTasks_SkipsDisabledTasks()
        {
            MethodInfo method = typeof(BuildSettingsUtility).GetMethod("ExecutePostBuildTasks", BindingFlags.NonPublic | BindingFlags.Instance);

            var profile = new ManagedBuildProfile();
            var enabledTask = new MockTask { IsEnabled = true };
            var disabledTask = new MockTask { IsEnabled = false };
            
            profile.PostBuildTasks.Add(enabledTask);
            profile.PostBuildTasks.Add(disabledTask);

            method.Invoke(_utility, new object[] { profile, "test/path" });

            Assert.IsTrue(enabledTask.WasExecuted, "Enabled task should have been executed.");
            Assert.IsFalse(disabledTask.WasExecuted, "Disabled task should not have been executed.");
        }

        [Test]
        public void ExecutePostBuildTasks_CatchesAndLogsExceptions()
        {
            MethodInfo method = typeof(BuildSettingsUtility).GetMethod("ExecutePostBuildTasks", BindingFlags.NonPublic | BindingFlags.Instance);

            var profile = new ManagedBuildProfile();
            var exceptionTask = new MockTask { IsEnabled = true, ThrowException = true };
            var secondTask = new MockTask { IsEnabled = true };
            
            profile.PostBuildTasks.Add(exceptionTask);
            profile.PostBuildTasks.Add(secondTask);

            // Expect a log warning rather than an unhandled exception thrown upwards
            // We use Regex matching (?i)failed to catch "Post-build task MockTask failed" 
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("(?i)failed"));

            Assert.DoesNotThrow(() => method.Invoke(_utility, new object[] { profile, "test/path" }), 
                "ExecutePostBuildTasks should catch task exceptions.");

            Assert.IsTrue(secondTask.WasExecuted, "Execution should continue for subsequent tasks after an exception.");
        }
    }
}
