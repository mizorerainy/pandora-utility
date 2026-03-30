using NUnit.Framework;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using MizoreRainy.Pandora.ConfigUtility.Editor;
using UnityEngine;
using MizoreRainy.Pandora.ConfigUtility;

namespace MizoreRainy.Pandora.Tests.Editor
{
    public class ConfigValidatorTests
    {
        // Dummy class with an unsupported type for testing validator
        public class UnsupportedClass {}

        public class DummyConfigHolder
        {
            [Config("InvalidKey", null, "")]
            public static readonly ConfigEntry<UnsupportedClass> InvalidEntry;
        }

        [Test]
        public void ConfigValidator_DetectsUnsupportedTypes_WithoutCrashing()
        {
            // The TypeCache inside ConfigValidator.ValidateConfigTypes() will pick up DummyConfigHolder.
            // If the validator works correctly, it should iterate over it, log a warning, and not throw an exception.
            // We use LogAssert to explicitly expect the warning.
            
            LogAssert.Expect(LogType.Warning, new Regex("Uses complex type .*?'UnsupportedClass'.*?"));
            
            Assert.DoesNotThrow(() => ConfigValidator.ValidateConfigTypes());
        }
    }
}
