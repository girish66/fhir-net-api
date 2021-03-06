﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using Hl7.Fhir.Support;
using Hl7.Fhir.Model;
using Hl7.Fhir.Test;
using Hl7.Fhir.Serialization;
using Ionic.Zip;

namespace Hl7.Fhir.Tests
{
    [TestClass]
    public class RoundtripTest
    {    
        [TestMethod]
        public void FullRoundtripOfAllExamples()
        {
            string examplesXml = @"..\..\data\examples.zip";
            //string examplesJson = @"..\..\data\examples-json.zip";

            // Create an empty temporary directory for us to dump the roundtripped intermediary files in
            string baseTestPath = Path.Combine(Path.GetTempPath(), "FHIRRoundTripTest");
            createEmptyDir(baseTestPath);

            Debug.WriteLine("First, roundtripping xml->json->xml");
            var baseTestPathXml = Path.Combine(baseTestPath, "FromXml");
            createEmptyDir(baseTestPathXml);
            doRoundTrip(examplesXml, baseTestPathXml);

            //Debug.WriteLine("Then, roundtripping json->xml->json");
            //var baseTestPathJson = Path.Combine(baseTestPath, "FromJson");
            //createEmptyDir(baseTestPathJson);
            //doRoundTrip(examplesJson, baseTestPathJson);

        }

        private static void createEmptyDir(string baseTestPath)
        {
            if (Directory.Exists(baseTestPath)) Directory.Delete(baseTestPath, true);
            Directory.CreateDirectory(baseTestPath);
        }

        private void doRoundTrip(string examplesZip, string baseTestPath)
        {
            var examplePath = Path.Combine(baseTestPath, "input");

            // Unzip files into this path
            Debug.WriteLine("Unzipping example files from {0} to {1}", examplesZip, examplePath);

            using (var zipfile = ZipFile.Read(examplesZip))
            {
                zipfile.ExtractAll(examplePath, ExtractExistingFileAction.OverwriteSilently);
            }
       
            var intermediate1Path = Path.Combine(baseTestPath, "intermediate1");
            Debug.WriteLine("Converting files in {0} to {1}", baseTestPath, intermediate1Path);
            convertFiles(examplePath, intermediate1Path);
            var intermediate2Path = Path.Combine(baseTestPath, "intermediate2");
            Debug.WriteLine("Re-converting files in {0} back to original format in {1}", intermediate1Path, intermediate2Path);
            convertFiles(intermediate1Path, intermediate2Path);
            Debug.WriteLine("Comparing files in {0} to files in {1}", baseTestPath, intermediate2Path);
            compareFiles(examplePath, intermediate2Path);
        }


        private void convertFiles(string inputPath, string outputPath)
        {
            var files = Directory.EnumerateFiles(inputPath);
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            foreach (string file in files)
            {
                string exampleName = Path.GetFileNameWithoutExtension(file);
                string ext = Path.GetExtension(file);
                var toExt = ext == ".xml" ? ".json" : ".xml";
                string outputFile = Path.Combine(outputPath, exampleName) + toExt;

                Debug.WriteLine("Converting {0} [{1}->{2}] ",exampleName,ext,toExt);

                if (!isFeed(file))
                    convertResource(file, outputFile);
                else
                    convertFeed(file, outputFile);
            }

            Debug.WriteLine("Done!");
        }


        private void compareFiles(string expectedPath, string actualPath)
        {
            var files = Directory.EnumerateFiles(expectedPath);

            foreach (string file in files)
            {
                string exampleName = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file);
                string actualFile = Path.Combine(actualPath, exampleName) + extension;

                if (!File.Exists(actualFile))
                    Assert.Fail("File {0}.{1} was not converted and not found in {2}", exampleName, extension,
                                        actualPath);

                Debug.WriteLine("Comparing " + exampleName);

                compareFile(file, actualFile);
            }
        }

        private void compareFile(string expectedFile, string actualFile)
        {
            if(expectedFile.EndsWith(".xml"))
                XmlAssert.AreSame(File.ReadAllText(expectedFile),File.ReadAllText(actualFile));
            else
                JsonAssert.AreSame(File.ReadAllText(expectedFile), File.ReadAllText(actualFile));
        }

        private bool isFeed(string filename)
        {
            var buffer = new char[250];

            using (var reader = new StreamReader(filename))
            {
                reader.Read(buffer, 0, buffer.Length);
                var data = new String(buffer);

                if (data.Contains("<feed")) return true;
                if (data.Contains("resourceType") && data.Contains("Bundle")) return true;

                return false;
            }
        }

      
        private void convertResource(string inputFile, string outputFile)
        {
            //TODO: call validation after reading

            if (inputFile.EndsWith(".xml"))
            {
                var xml = File.ReadAllText(inputFile);
                var resource = FhirParser.ParseResourceFromXml(xml);

                var json = FhirSerializer.SerializeResourceToJson(resource);
                File.WriteAllText(outputFile, json);
            }
            else
            {
                var json = File.ReadAllText(inputFile);
                var resource = FhirParser.ParseResourceFromJson(json);
                var xml = FhirSerializer.SerializeResourceToXml(resource);
                File.WriteAllText(outputFile, xml);
            }
        }

        private void convertFeed(string inputFile, string outputFile)
        {
            //TODO: call validation after reading

            if (inputFile.EndsWith(".xml"))
            {
                var xml = File.ReadAllText(inputFile);
                var resource = FhirParser.ParseBundleFromXml(xml);

                var json = FhirSerializer.SerializeBundleToJson(resource);
                File.WriteAllText(outputFile, json);
            }
            else
            {
                var json = File.ReadAllText(inputFile);
                var resource = FhirParser.ParseBundleFromJson(json);
                var xml = FhirSerializer.SerializeBundleToXml(resource);
                File.WriteAllText(outputFile, xml);
            }
        }
    }
}
