using NUnit.Framework;
using MizoreRainy.Pandora.ConfigUtility;

namespace MizoreRainy.Pandora.Tests.Runtime
{
    public class ConfigRegistryTests
    {
        [SetUp]
        public void Setup()
        {
            // Reset registry before each test
            ConfigLoader.Registry = new ConfigRegistry();
        }

        [Test]
        public void ConfigEntry_Initialization_SetsDefaultValue()
        {
            var attr = new ConfigAttribute("TestKey", 42, "Description");
            var entry = new ConfigEntry<int>(attr, "TestGroup");

            Assert.AreEqual("TestKey", entry.Key);
            Assert.AreEqual("TestGroup", entry.GroupName);
            Assert.AreEqual(42, entry.Value);
        }

        [Test]
        public void ConfigEntry_SetValue_TriggersOnChangeEvent()
        {
            var attr = new ConfigAttribute("TestKey", 0, "");
            var entry = new ConfigEntry<int>(attr, "TestGroup");

            bool triggered = false;
            int receivedValue = -1;

            entry.OnChange(val => 
            {
                triggered = true;
                receivedValue = val;
            });

            entry.SetValue(99);

            Assert.IsTrue(triggered, "OnChange event was not triggered.");
            Assert.AreEqual(99, receivedValue, "Event received incorrect value.");
            Assert.AreEqual(99, entry.Value, "Entry value was not updated.");
        }

        [Test]
        public void ConfigEntry_SetToDefault_RevertsValue()
        {
            var attr = new ConfigAttribute("TestKey", 50, "");
            var entry = new ConfigEntry<int>(attr, "TestGroup");
            var iEntry = (IConfigEntry)entry;

            entry.SetValue(100);
            Assert.AreEqual(100, entry.Value);

            iEntry.SetToDefault();
            Assert.AreEqual(50, entry.Value, "Value did not revert to default.");
        }

        [Test]
        public void ConfigEntry_SetValueFromString_ParsesCorrectly()
        {
            var attr = new ConfigAttribute("TestKey", 0, "");
            var entry = new ConfigEntry<int>(attr, "TestGroup");
            var iEntry = (IConfigEntry)entry;

            iEntry.SetValueFromString("123");
            Assert.AreEqual(123, entry.Value);
        }
    }
}
