using System;
using NUnit.Framework;
using MizoreRainy.Pandora.ConfigUtility.Parsers;
using UnityEngine;

namespace MizoreRainy.Pandora.Tests.Runtime
{
    public class ParserTests
    {
        private ArrayConfigParser _parser;

        [SetUp]
        public void Setup()
        {
            _parser = new ArrayConfigParser();
        }

        [Test]
        public void ArrayConfigParser_CanParseCommaSeparatedInts()
        {
            var rawValue = "[1, 2, 3]";
            var type = typeof(int[]);
            
            bool success = _parser.TryParse(rawValue, type, out object result);
            
            Assert.IsTrue(success, "Parser failed to parse the integer array.");
            
            int[] typedResult = result as int[];
            Assert.IsNotNull(typedResult, "Result was not of type int[].");
            Assert.AreEqual(3, typedResult.Length);
            Assert.AreEqual(1, typedResult[0]);
            Assert.AreEqual(2, typedResult[1]);
            Assert.AreEqual(3, typedResult[2]);
        }

        [Test]
        public void ArrayConfigParser_ToString_FormatsCorrectly()
        {
            int[] val = new[] { 5, 10, 15 };
            
            string stringVal = _parser.ToString(val);
            
            Assert.AreEqual("[5, 10, 15]", stringVal);
        }

        [Test]
        public void ArrayConfigParser_CanParseVector3Array()
        {
            var rawValue = "[\"(1, 2, 3)\", \"(4, 5, 6)\"]";
            var type = typeof(Vector3[]);
            
            bool success = _parser.TryParse(rawValue, type, out object result);
            
            Assert.IsTrue(success);
            Vector3[] typedResult = result as Vector3[];
            Assert.IsNotNull(typedResult);
            Assert.AreEqual(2, typedResult.Length);
            Assert.AreEqual(new Vector3(1, 2, 3), typedResult[0]);
            Assert.AreEqual(new Vector3(4, 5, 6), typedResult[1]);
        }
    }
}
