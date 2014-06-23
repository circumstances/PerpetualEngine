using NUnit.Framework;
using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

namespace PerpetualEngine.Storage
{
    [TestFixture()]
    public class JsonPersistingListTests
    {
        string editGroup;

        [SetUp]
        public void Setup()
        {
            editGroup = Guid.NewGuid().ToString();
        }

        [Test()]
        public void TestStoringAndLoading()
        {
            var list = new JsonPersistingList<IdentifiableForTesting>(editGroup);
            list.Add(new IdentifiableForTesting("test"));
            Assert.AreEqual("{\"Id\":\"test\"}", SimpleStorage.EditGroup(editGroup).Get("test"));

            list = new JsonPersistingList<IdentifiableForTesting>(editGroup);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("test", list.First().Id);
        }

        [Test()]
        public void TestSkippingNonDeserializableEntries()
        {
            var list = new JsonPersistingList<IdentifiableForTesting>(editGroup);
            list.Add(new IdentifiableForTesting("1"));
            list.Add(new IdentifiableForTesting("2"));
            list.Add(new IdentifiableForTesting("3"));

            var storage = SimpleStorage.EditGroup(editGroup);
            storage.Put("2", "some bad data");

            list = new JsonPersistingList<IdentifiableForTesting>(editGroup);

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("1", list.First().Id);
            Assert.AreEqual("3", list.Skip(1).First().Id);
        }
    }
}