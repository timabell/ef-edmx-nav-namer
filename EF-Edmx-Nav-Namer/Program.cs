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

        private SortMethod _sortMethod;

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
            var sortMethodArg = ((ValueArgument<string>)parser.LookupArgument("sort")).Value;
            var sortMethod = SortMethod.StorageModel;
            if (sortMethodArg !=null && !Enum.TryParse(sortMethodArg, true, out sortMethod))
            {
                Console.Error.WriteLine("Invalid sort type");
                parser.ShowUsage();
            }

            var p = new Program(inputFileName, sortMethod);

            p.ReorderConceptualProperties();
        }

        private static CommandLineParser.CommandLineParser CreateParser()
        {
            var parser = new CommandLineParser.CommandLineParser { IgnoreCase = true };

            parser.Arguments.Add(new FileArgument('i', "input", "original edmx file") { FileMustExist = true, Optional = false });
            parser.Arguments.Add(new ValueArgument<string>('s', "sort", "how to sort the properties, None, Alphabetical or StorageModel (default)"));

            return parser;
        }

        public String InputFileName { get; set; }

        public Program(string inputFileName, SortMethod sortMethod)
        {
            InputFileName = inputFileName;
            _sortMethod = sortMethod;
        }

        private void ReorderConceptualProperties()
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
                switch (_sortMethod)
                {
                    case SortMethod.None:
                        ReorderElements(entity, (x) => x, "Property");
                        break;
                    case SortMethod.Alphabetical:
                        ReorderElements(entity, AlphabeticalSorter, "Property");
                        break;
                    case SortMethod.StorageModel:
                        ApplyStorageSort(entity, storageEntities);
                        break;
                    default:
                        throw new NotImplementedException(string.Format("Unknown sort method {0}", _sortMethod));
                }
                // move navigation properties to end, leaving in original order
                ReorderElements(entity, (x) => x, "NavigationProperty");
            }

            Console.WriteLine("Writing result to {0}", InputFileName);
            if (File.Exists(InputFileName))
            {
                File.Delete(InputFileName);
            }
            doc.Save(InputFileName);
        }

        private static void ApplyStorageSort(XElement entity, IEnumerable<XElement> storageEntities)
        {
            var entityName = entity.NameAttribute();
            var storageEntity = storageEntities.SingleOrDefault(s => s.NameAttribute() == entityName);
            if (storageEntity == null)
            {
                Console.Error.WriteLine("{0} exists in conceptual model but not in storage model, skipped.", entityName);
                return;
            }
            var storageProps = storageEntity.FindByLocalName("Property");
            ReorderElements(entity, StorageSorter(storageProps), "Property");
        }

        private static Func<IEnumerable<XElement>, IEnumerable<XElement>> StorageSorter(IEnumerable<XElement> storageProps)
        {
            return (input) => StorageSorterInner(input, storageProps);
        }

        private static IEnumerable<XElement> StorageSorterInner(IEnumerable<XElement> input, IEnumerable<XElement> storageProps)
        {
            var output = new List<XElement>();
            var hitList = input.ToList();
            foreach (var storageProp in storageProps)
            {
                var hit = hitList.SingleOrDefault(x => x.NameAttribute() == storageProp.NameAttribute());
                if (hit == null)
                {
                    // storage prop has no matching conceptual property, ignore
                    continue;
                }
                output.Add(hit);
                hitList.Remove(hit);
            }
            // put anything that wasn't found in the storage model at the end just so we don't drop it completely
            output.AddRange(hitList);
            return output;
        }

        private static IEnumerable<XElement> AlphabeticalSorter(IEnumerable<XElement> input)
        {
            return input.OrderBy(p => p.NameAttribute());
        }

        private static void ReorderElements(XContainer entity, Func<IEnumerable<XElement>, IEnumerable<XElement>> sorter, string elementName)
        {
            var props = entity.FindByLocalName(elementName).ToList();
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
