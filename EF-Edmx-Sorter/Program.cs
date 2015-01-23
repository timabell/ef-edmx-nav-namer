using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CommandLineParser.Arguments;
using CommandLineParser.Exceptions;

namespace EfEdmxSorter
{

    class Program
    {
        static void Main(string[] args)
        {
            var parser = CreateParser();

            try
            {
                parser.ParseCommandLine(args);
            }
            catch (CommandLineException e)
            {
                Console.WriteLine(e.Message);
                parser.ShowUsage();
                return;
            }


            var inputFileName = ((FileArgument)parser.LookupArgument("input")).Value.FullName;

            var p = new Program(inputFileName);

            p.ReorderConceptualProperties();
        }

        private static CommandLineParser.CommandLineParser CreateParser()
        {
            var parser = new CommandLineParser.CommandLineParser { IgnoreCase = true };

            parser.Arguments.Add(new FileArgument('i', "input", "original edmx file") { FileMustExist = true, Optional = false });

            return parser;
        }

        public String InputFileName { get; set; }

        public Program(String inputFileName)
        {
            InputFileName = inputFileName;
        }

        private void ReorderConceptualProperties()
        {
            // find the storage model, load all the tables and properties, remember the order
            // find the conceptual model, re-order all the properties to match the storage model

            var doc = LoadEdmx();

            var storageModel = doc.FindByLocalName("StorageModels").First();

            var conceptualModel = doc.FindByLocalName("ConceptualModels").First();
            var entities = conceptualModel.FindByLocalName("EntityType");
            foreach (var entity in entities)
            {
                ReorderProperties(entity);
            }

            Console.WriteLine("Writing result to {0}", InputFileName);
            if (File.Exists(InputFileName))
            {
                File.Delete(InputFileName);
            }
            doc.Save(InputFileName);
        }

        private static void ReorderProperties(XElement entity)
        {
            var props = entity.FindByLocalName("Property").ToList();
            // clear
            props.Remove();
            // re-add in new order, will be added to end
            foreach (var prop in props.OrderBy(p => p.Attribute("Name").Value))
            {
                entity.Add(prop);
            }
        }

        private XDocument LoadEdmx()
        {
            var doc = XDocument.Load(InputFileName);

            if (doc.Root == null)
            {
                throw new Exception(string.Format("Loaded XDocument Root is null. File: {0}", InputFileName));
            }
            return doc;
        }

        private void AddNodeDocumentation(XElement element, String documentation)
        {
            // remove stale documentation
            element.FindByLocalName("Documentation").Remove();

            if (String.IsNullOrEmpty(documentation))
                return;
            var xmlns = element.GetDefaultNamespace();

            element.AddFirst(new XElement(xmlns + "Documentation", new XElement(xmlns + "Summary", documentation)));
        }
    }
}
