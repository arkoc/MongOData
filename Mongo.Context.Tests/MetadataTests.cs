﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
using NUnit.Framework;
using Simple.Data;
using Simple.Data.OData;

namespace Mongo.Context.Tests
{
    public abstract class MetadataTests<T>
    {
        protected TestService service;
        protected dynamic ctx;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            TestData.PopulateWithVariableTypes();
            service = new TestService(typeof(T));
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            if (service != null)
            {
                service.Dispose();
                service = null;
            }

            TestData.Clean();
        }

        [SetUp]
        public void SetUp()
        {
            ProductInMemoryService.ResetDSPMetadata();
            ProductQueryableService.ResetDSPMetadata();
            ctx = Database.Opener.Open(service.ServiceUri);
        }

        protected void ResetService()
        {
            service.Dispose();
            service = new TestService(typeof(T));
            ctx = Database.Opener.Open(service.ServiceUri);
        }

        [Test]
        public void Metadata()
        {
            var request = (HttpWebRequest)WebRequest.Create(service.ServiceUri + "/$metadata");
            var response = (HttpWebResponse)request.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "The $metadata didn't return success.");
        }

        [Test]
        public void VariableTypesPrefetchOneNoUpdate()
        {
            TestService.Configuration = new MongoConfiguration { MetadataBuildStrategy = new MongoConfiguration.Metadata { PrefetchRows = 1, UpdateDynamically = false } };
            ResetService();

            var result = ctx.VariableTypes.All().ToList();
            AssertResultHasOneColumn(result);
        }

        [Test]
        public void VariableTypesPrefetchTwoNoUpdate()
        {
            TestService.Configuration = new MongoConfiguration { MetadataBuildStrategy = new MongoConfiguration.Metadata { PrefetchRows = 2, UpdateDynamically = false } };
            ResetService();

            var result = ctx.VariableTypes.All().ToList();
            AssertResultHasTwoColumns(result);
        }

        [Test]
        public void VariableTypesPrefetchAll()
        {
            TestService.Configuration = new MongoConfiguration { MetadataBuildStrategy = new MongoConfiguration.Metadata { PrefetchRows = -1, UpdateDynamically = false } };
            ResetService();

            var result = ctx.VariableTypes.All().ToList();
            AssertResultHasThreeColumns(result);
        }

        protected void AssertResultHasNoColumns(IList<dynamic> result)
        {
            Assert.Throws<RuntimeBinderException>(() => { var x = result[0].StringValue; });
        }

        protected void AssertResultHasOneColumn(IList<dynamic> result)
        {
            Assert.AreEqual("1", result[0].StringValue);
            Assert.Throws<RuntimeBinderException>(() => { var x = result[1].IntValue; });
        }

        protected void AssertResultHasTwoColumns(IList<dynamic> result)
        {
            Assert.AreEqual("1", result[0].StringValue);
            Assert.AreEqual(2, result[1].IntValue);
            Assert.Throws<RuntimeBinderException>(() => { var x = result[2].DecimalValue; });
        }

        protected void AssertResultHasThreeColumns(IList<dynamic> result)
        {
            Assert.AreEqual("1", result[0].StringValue);
            Assert.AreEqual(2, result[1].IntValue);
            Assert.AreEqual("3", result[2].DecimalValue);
        }
    }

    [TestFixture]
    public class InMemoryServiceMetadataTests : MetadataTests<ProductInMemoryService>
    {
    }

    [TestFixture]
    public class QueryableServiceMetadataTests : MetadataTests<ProductQueryableService>
    {
        [Test]
        public void VariableTypesPrefetchNoneUpdate()
        {
            TestService.Configuration = new MongoConfiguration { MetadataBuildStrategy = new MongoConfiguration.Metadata { PrefetchRows = 0, UpdateDynamically = true } };
            ResetService();

            var result = ctx.VariableTypes.All().ToList();
            AssertResultHasNoColumns(result);
            ResetService();

            result = ctx.VariableTypes.All().ToList();
            AssertResultHasThreeColumns(result);
        }

        [Test]
        public void VariableTypesPrefetchOneUpdate()
        {
            TestService.Configuration = new MongoConfiguration { MetadataBuildStrategy = new MongoConfiguration.Metadata { PrefetchRows = 1, UpdateDynamically = true } };
            ResetService();

            var result = ctx.VariableTypes.All().ToList();
            AssertResultHasOneColumn(result);
            ResetService();

            result = ctx.VariableTypes.All().ToList();
            AssertResultHasThreeColumns(result);
        }

        [Test]
        public void VariableTypesPrefetchTwoUpdate()
        {
            TestService.Configuration = new MongoConfiguration { MetadataBuildStrategy = new MongoConfiguration.Metadata { PrefetchRows = 2, UpdateDynamically = true } };
            ResetService();

            var result = ctx.VariableTypes.All().ToList();
            AssertResultHasTwoColumns(result);
            ResetService();

            result = ctx.VariableTypes.All().ToList();
            AssertResultHasThreeColumns(result);
        }
    }
}
