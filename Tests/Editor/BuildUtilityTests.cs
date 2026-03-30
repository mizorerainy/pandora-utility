using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using MizoreRainy.Pandora.BuildUtility;
using System;

namespace MizoreRainy.Pandora.Tests.Editor
{
    public class BuildUtilityTests
    {
        private BuildSettingsUtility _utility;

        [SetUp]
        public void Setup()
        {
            _utility = ScriptableObject.CreateInstance<BuildSettingsUtility>();
        }

        [Test]
        public void IsValidVersionFormat_ValidatesMajorMinorPatch()
        {
            MethodInfo method = typeof(BuildSettingsUtility).GetMethod("IsValidVersionFormat", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "IsValidVersionFormat method not found.");

            // Valid
            Assert.IsTrue((bool)method.Invoke(_utility, new object[] { "1.0.0" }));
            Assert.IsTrue((bool)method.Invoke(_utility, new object[] { "10.0.99" }));

            // Invalid
            Assert.IsFalse((bool)method.Invoke(_utility, new object[] { "1.0" }));
            Assert.IsFalse((bool)method.Invoke(_utility, new object[] { "1.0.0.0" }));
            Assert.IsFalse((bool)method.Invoke(_utility, new object[] { "v1.0.0" }));
            Assert.IsFalse((bool)method.Invoke(_utility, new object[] { "1.0.a" }));
            Assert.IsFalse((bool)method.Invoke(_utility, new object[] { "" }));
            Assert.IsFalse((bool)method.Invoke(_utility, new object[] { null }));
        }

        [Test]
        public void GetPlatformFolder_ReturnsCorrectStringForTarget()
        {
            MethodInfo method = typeof(BuildSettingsUtility).GetMethod("GetPlatformFolder", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "GetPlatformFolder method not found.");

            Assert.AreEqual("PC", (string)method.Invoke(_utility, new object[] { BuildTarget.StandaloneWindows }));
            Assert.AreEqual("PC", (string)method.Invoke(_utility, new object[] { BuildTarget.StandaloneWindows64 }));
            Assert.AreEqual("PC", (string)method.Invoke(_utility, new object[] { BuildTarget.StandaloneOSX }));
            Assert.AreEqual("Android", (string)method.Invoke(_utility, new object[] { BuildTarget.Android }));
            Assert.AreEqual("iOS", (string)method.Invoke(_utility, new object[] { BuildTarget.iOS }));
            Assert.AreEqual("Other", (string)method.Invoke(_utility, new object[] { BuildTarget.WebGL }));
        }

        [Test]
        public void GetBuildExtension_ReturnsCorrectFileExtension()
        {
            MethodInfo method = typeof(BuildSettingsUtility).GetMethod("GetBuildExtension", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "GetBuildExtension method not found.");

            Assert.AreEqual(".exe", (string)method.Invoke(_utility, new object[] { BuildTarget.StandaloneWindows }));
            Assert.AreEqual(".exe", (string)method.Invoke(_utility, new object[] { BuildTarget.StandaloneWindows64 }));
            Assert.AreEqual(".app", (string)method.Invoke(_utility, new object[] { BuildTarget.StandaloneOSX }));
            Assert.AreEqual(".apk", (string)method.Invoke(_utility, new object[] { BuildTarget.Android }));
            Assert.AreEqual("", (string)method.Invoke(_utility, new object[] { BuildTarget.WebGL }));
        }
    }
}
