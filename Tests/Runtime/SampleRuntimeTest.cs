using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MizoreRainy.Pandora.Tests.Runtime
{
    public class SampleRuntimeTest
    {
        [Test]
        public void SampleRuntimeTestSimplePasses()
        {
            // Use the Assert class to test conditions
            Assert.IsTrue(true);
        }

        [UnityTest]
        public IEnumerator SampleRuntimeTestWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
            Assert.IsTrue(true);
        }
    }
}
