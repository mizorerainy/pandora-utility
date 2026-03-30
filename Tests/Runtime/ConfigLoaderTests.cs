using System.IO;
using System.Reflection;
using NUnit.Framework;
using MizoreRainy.Pandora.ConfigUtility;

namespace MizoreRainy.Pandora.Tests.Runtime
{
    public class ConfigLoaderTests
    {
        private string _tempIniPath;

        [SetUp]
        public void Setup()
        {
            ConfigLoader.Registry = new ConfigRegistry();
            _tempIniPath = Path.Combine(Path.GetTempPath(), "pandora_test_config.ini");
        }

        [TearDown]
        public void Teardown()
        {
            if (File.Exists(_tempIniPath))
            {
                File.Delete(_tempIniPath);
            }
        }

        [Test]
        public void LoadFromIniSync_ParsesKeyValuesCorrectly()
        {
            // Arrange
            var attr1 = new ConfigAttribute("TestKey1", "default", "");
            var entry1 = new ConfigEntry<string>(attr1, "Group");
            
            var attr2 = new ConfigAttribute("TestKey2", 0, "");
            var entry2 = new ConfigEntry<int>(attr2, "Group");

            ConfigLoader.Registry.Settings.Add(entry1);
            ConfigLoader.Registry.Settings.Add(entry2);

            File.WriteAllText(_tempIniPath, "TestKey1=LoadedValue\nTestKey2=99\n");

            // Act - Using reflection since LoadFromIniSync is private
            MethodInfo loadMethod = typeof(ConfigLoader).GetMethod("LoadFromIniSync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(loadMethod, "LoadFromIniSync method not found via reflection.");
            
            loadMethod.Invoke(null, new object[] { _tempIniPath });

            // Assert
            Assert.AreEqual("LoadedValue", entry1.Value);
            Assert.AreEqual(99, entry2.Value);
        }

        [Test]
        public void LoadFromIniSync_MissingKeysRevertToDefault()
        {
            // Arrange
            var attr = new ConfigAttribute("MissingKey", "default_val", "");
            var entry = new ConfigEntry<string>(attr, "Group");
            entry.SetValue("changed_val"); // Change from default

            ConfigLoader.Registry.Settings.Add(entry);

            File.WriteAllText(_tempIniPath, "OtherKey=SomethingElse\n");

            // Act
            MethodInfo loadMethod = typeof(ConfigLoader).GetMethod("LoadFromIniSync", BindingFlags.NonPublic | BindingFlags.Static);
            loadMethod.Invoke(null, new object[] { _tempIniPath });

            // Assert
            Assert.AreEqual("default_val", entry.Value, "Missing key in INI did not cause fallback to default.");
        }
    }
}
