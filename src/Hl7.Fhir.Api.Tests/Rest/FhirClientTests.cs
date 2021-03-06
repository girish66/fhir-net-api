﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;
using Hl7.Fhir.Model;
using System.Net;
using Hl7.Fhir.Support;
using Hl7.Fhir.Support.Search;
using System.IO;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using System.Threading.Tasks;

namespace Hl7.Fhir.Tests
{
   [TestClass]
    public class FhirClientTests
    {
     
        Uri testEndpoint = new Uri("http://spark.furore.com/fhir");
        //Uri testEndpoint = new Uri("http://localhost.fiddler:1396/fhir");
      //  Uri testEndpoint = new Uri("http://localhost:1396/fhir");
        //Uri testEndpoint = new Uri("http://fhir.healthintersections.com.au/open");
        //Uri testEndpoint = new Uri("https://api.fhir.me");

        [TestMethod]
        public void FetchConformance()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var entry = client.Conformance();
            var c = entry.Resource;

            Assert.IsNotNull(c);
            Assert.AreEqual("Spark.Service", c.Software.Name);
            Assert.AreEqual(Conformance.RestfulConformanceMode.Server, c.Rest[0].Mode.Value);
            Assert.AreEqual(HttpStatusCode.OK, client.LastResponseDetails.Result);

            // Does't currently work on Grahame's server
            //Assert.AreEqual(ContentType.XML_CONTENT_HEADER, client.LastResponseDetails.ContentType.ToLower());
        }


        [TestMethod]
        public void ReadWithFormat()
        {
            FhirClient client = new FhirClient(testEndpoint);

            client.UseFormatParam = true;
            client.PreferredFormat = ResourceFormat.Json;

            var loc = client.Read<Location>("1");

            Assert.AreEqual(ResourceFormat.Json, ContentType.GetResourceFormatFromContentType(client.LastResponseDetails.ContentType));
        }


        [TestMethod]
        public void Read()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var loc = client.Read<Location>("1");
            Assert.IsNotNull(loc);
            Assert.AreEqual("Den Burg", loc.Resource.Address.City);

            string version = new ResourceIdentity(loc.SelfLink).VersionId;
            Assert.IsNotNull(version);
            string id = new ResourceIdentity(loc.Id).Id;
            Assert.AreEqual("1", id);

            try
            {
                var random = client.Read<Location>("45qq54");
                Assert.Fail();
            }
            catch (FhirOperationException)
            {
                Assert.IsTrue(client.LastResponseDetails.Result == HttpStatusCode.NotFound);
            }

            //Currently fails on Grahame's server
            //var loc2 = client.Read<Location>("1", version);
            //Assert.IsNotNull(loc2);
            //Assert.AreEqual(FhirSerializer.SerializeBundleEntryToJson(loc),
            //                FhirSerializer.SerializeBundleEntryToJson(loc2));

            //var loc3 = client.Read<Location>(loc.SelfLink);
            //Assert.IsNotNull(loc3);
            //Assert.AreEqual(FhirSerializer.SerializeBundleEntryToJson(loc),
            //                FhirSerializer.SerializeBundleEntryToJson(loc3));

        }


        [TestMethod]
        public void Search()
        {
            FhirClient client = new FhirClient(testEndpoint);
            Bundle result;

            result = client.Search<DiagnosticReport>();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entries.Count() > 10, "Test should use testdata with more than 10 reports");

            result = client.Search<DiagnosticReport>(count: 10);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entries.Count <= 10);

            var withSubject = 
                result.Entries.ByResourceType<DiagnosticReport>().FirstOrDefault(dr => dr.Resource.Subject != null);
            Assert.IsNotNull(withSubject, "Test should use testdata with a report with a subject");

            ResourceIdentity ri = new ResourceIdentity(withSubject.Id);

            result = client.SearchById<DiagnosticReport>(ri.Id, 
                        includes: new string[] { "DiagnosticReport.subject" });
            Assert.IsNotNull(result);

            // Doesn't currently work on Grahames server           
            //Assert.AreEqual(2, result.Entries.Count);  // should have subject too

            //Assert.IsNotNull(result.Entries.Single(entry => new ResourceIdentity(entry.Id).Collection ==
            //            typeof(DiagnosticReport).GetCollectionName()));
            //Assert.IsNotNull(result.Entries.Single(entry => new ResourceIdentity(entry.Id).Collection ==
            //            typeof(Patient).GetCollectionName()));

            result = client.Search<Patient>(new SearchParam[] 
                {
                    new SearchParam("name", "Everywoman"),
                    new SearchParam("name", "Eve") 
                });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entries.Count > 0);
        }

        [TestMethod]
        public void Paging()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var result = client.Search<DiagnosticReport>(count: 10);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entries.Count <= 10);

            var firstId = result.Entries.First().Id;

            // Browse forward
            result = client.Continue(result);
            Assert.IsNotNull(result);
            var nextId = result.Entries.First().Id;
            Assert.AreNotEqual(firstId, nextId);

            // Browse to first
            result = client.Continue(result, PageDirection.First);
            Assert.IsNotNull(result);
            var prevId = result.Entries.First().Id;
            Assert.AreEqual(firstId, prevId);

            // Forward, then backwards
            // Does not work on Grahame's server yet
            //Assert.IsNotNull(client.Continue(result,PageDirection.Next));
            //result = client.Continue(result, PageDirection.Previous);
            //Assert.IsNotNull(result);
            //prevId = result.Entries.First().Id;
            //Assert.AreEqual(firstId, prevId);
        }



        //private Uri createdTestOrganization = null;

        [TestMethod]
        public void CreateEditDelete()
        {
            var furore = new Organization
            {
                Name = "Furore",
                Identifier = new List<Identifier> { new Identifier("http://hl7.org/test/1", "3141") },
                Telecom = new List<Contact> { new Contact { System = Contact.ContactSystem.Phone, Value = "+31-20-3467171" } }
            };

            FhirClient client = new FhirClient(testEndpoint);
            var tags = new List<Tag> { new Tag("http://nu.nl/testname", Tag.FHIRTAGSCHEME_GENERAL, "TestCreateEditDelete") };

            var fe = client.Create<Organization>(furore, tags:tags);

            Assert.IsNotNull(furore);
            Assert.IsNotNull(fe);
            Assert.IsNotNull(fe.Id);
            Assert.IsNotNull(fe.SelfLink);
            Assert.AreNotEqual(fe.Id, fe.SelfLink);
          //  createdTestOrganization = fe.Id;

            fe.Resource.Identifier.Add(new Identifier("http://hl7.org/test/2", "3141592"));

            var fe2 = client.Update(fe);

            Assert.IsNotNull(fe2);
            Assert.AreEqual(fe.Id, fe2.Id);
            Assert.AreNotEqual(fe.SelfLink, fe2.SelfLink);

            //TODO: Fix this bug (Issue #11 in Github)
            //Assert.IsNotNull(fe2.Tags);
            //Assert.AreEqual(1, fe2.Tags.Count());
            //Assert.AreEqual(fe2.Tags.First(), tags[0]);

            client.Delete(fe2);

            try
            {
                fe = client.Read<Organization>(new ResourceIdentity(fe.Id).Id);
                Assert.Fail();
            }
            catch
            {
                Assert.IsTrue(client.LastResponseDetails.Result == HttpStatusCode.Gone);
            }
        }


        //[TestMethod]
        //public void History()
        //{
        //    DateTimeOffset now = DateTimeOffset.Now;

        //    CreateEditDelete();

        //    FhirClient client = new FhirClient(testEndpoint);
        //    Bundle history = client.History(createdTestOrganization);
        //    Assert.IsNotNull(history);
        //    Assert.AreEqual(3, history.Entries.Count());
        //    Assert.AreEqual(2, history.Entries.Where(entry => entry is ResourceEntry).Count());
        //    Assert.AreEqual(1, history.Entries.Where(entry => entry is DeletedEntry).Count());

        //    // Now, assume no one is quick enough to insert something between now and the next
        //    // tests....

        //    history = client.History<Organization>(now);
        //    Assert.IsNotNull(history);
        //    Assert.AreEqual(3, history.Entries.Count());
        //    Assert.AreEqual(2, history.Entries.Where(entry => entry is ResourceEntry).Count());
        //    Assert.AreEqual(1, history.Entries.Where(entry => entry is DeletedEntry).Count());

        //    history = client.History(now);
        //    Assert.IsNotNull(history);
        //    Assert.AreEqual(3, history.Entries.Count());
        //    Assert.AreEqual(2, history.Entries.Where(entry => entry is ResourceEntry).Count());
        //    Assert.AreEqual(1, history.Entries.Where(entry => entry is DeletedEntry).Count());
        //}


        //[TestMethod]
        //public void ParseForPPT()
        //{
        //    ErrorList errors = new ErrorList();

        //    // Create a file-based reader for Xml
        //    XmlReader xr = XmlReader.Create(
        //        new StreamReader(@"publish\observation-example.xml"));

        //    // Create a file-based reader for Xml
        //    var obs = (Observation)FhirParser.ParseResource(xr, errors);

        //    // Modify some fields of the observation
        //    obs.Status = ObservationStatus.Amended;
        //    obs.Value = new Quantity() { Value = 40, Units = "g" };

        //    // Serialize the in-memory observation to Json
        //    var jsonText = FhirSerializer.SerializeResourceToJson(obs);

        //}


        [TestMethod]
        public void ReadTags()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var tags = new List<Tag>() { new Tag("http://readtag.nu.nl", Tag.FHIRTAGSCHEME_GENERAL, "readTagTest") };

            client.AffixTags<Location>(tags, "1");
            var affixedEntry = client.Read<Location>("1");
            var version = new ResourceIdentity(affixedEntry.SelfLink).VersionId;
            var list = client.GetTags();
            Assert.IsTrue(list.Any(t => t.Equals(tags.First())));

            list = client.GetTags<Location>();
            Assert.IsTrue(list.Any(t => t.Equals(tags.First())));

            list = client.GetTags<Location>("1", version);
            Assert.IsTrue(list.Any(t => t.Equals(tags.First())));

            client.DeleteTags<Location>(tags, "1", version);
        }
    }
}
