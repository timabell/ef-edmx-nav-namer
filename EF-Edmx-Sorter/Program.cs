using System;
using System.Collections.Generic;
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

            p.ReorderConceptualProperties(SortMethod.StorageModel);
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

        private void ReorderConceptualProperties(SortMethod sortMethod)
        {
            // find the storage model, load all the tables and properties, remember the order
            // find the conceptual model, re-order all the properties to match the storage model

            var doc = LoadEdmx();

            var storageModel = doc.FindByLocalName("StorageModels").First();
            var storageEntities = storageModel.FindByLocalName("EntityType").ToList();

            var conceptualModel = doc.FindByLocalName("ConceptualModels").First();
            var entities = conceptualModel.FindByLocalName("EntityType");
            foreach (var entity in entities)
            {
                switch (sortMethod)
                {
                    case SortMethod.Alphabetical:
                        ReorderProperties(entity, AlphabeticalSorter);
                        break;
                    case SortMethod.StorageModel:
                        ApplyStorageSort(entity, storageEntities);
                        break;
                    default:
                        throw new NotImplementedException(string.Format("Unknown sort method {0}", sortMethod));
                }
            }

            Console.WriteLine("Writing result to {0}", InputFileName);
            if (File.Exists(InputFileName))
            {
                File.Delete(InputFileName);
            }
            doc.Save(InputFileName);
        }

        private static void ApplyStorageSort(XElement entity, List<XElement> storageEntities)
        {
            var entityName = entity.NameAttribute();
            var storageEntity = storageEntities.SingleOrDefault(s => s.NameAttribute() == entityName);
            if (storageEntity == null)
            {
                Console.Error.WriteLine("{0} exists in conceptual model but not in storage model, skipped.", entityName);
                return;
            }
            var storageProps = storageEntity.FindByLocalName("Property");
            ReorderProperties(entity, StorageSorter(storageProps));
        }

        private static Func<IEnumerable<XElement>, IEnumerable<XElement>> StorageSorter(IEnumerable<XElement> storageProps)
        {
            return (input) => StorageSorterInner(input, storageProps);
        }

        private static IEnumerable<XElement> StorageSorterInner(IEnumerable<XElement> input, IEnumerable<XElement> storageProps)
        {
            // todo: use storage props as sort source
            return input.OrderByDescending(p => p.NameAttribute());
        }

        private static IEnumerable<XElement> AlphabeticalSorter(IEnumerable<XElement> input)
        {
            return input.OrderBy(p => p.NameAttribute());
        }

        private static void ReorderProperties(XContainer entity, Func<IEnumerable<XElement>, IEnumerable<XElement>> sorter)
        {
            var props = entity.FindByLocalName("Property").ToList();
            // clear
            props.Remove();
            // re-add in new order, will be added to end
            foreach (var prop in sorter(props).ToList())
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
    }
}
