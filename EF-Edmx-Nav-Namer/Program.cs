using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CommandLineParser.Arguments;
using CommandLineParser.Exceptions;

namespace EfEdmxNavNamer
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

            p.RenameNavigationProperties();
        }

        private static CommandLineParser.CommandLineParser CreateParser()
        {
            var parser = new CommandLineParser.CommandLineParser { IgnoreCase = true };

            parser.Arguments.Add(new FileArgument('i', "input", "original edmx file") { FileMustExist = true, Optional = false });

            return parser;
        }

        public String InputFileName { get; set; }

        public Program(string inputFileName)
        {
            InputFileName = inputFileName;
        }

        private void RenameNavigationProperties()
        {
            // find the storage model, load all the tables and properties, remember the order
            // find the conceptual model, re-order all the properties to match the storage model

            var doc = LoadEdmx();

            var conceptualModel = doc.FindByLocalName("ConceptualModels").First();
            var entities = conceptualModel.FindByLocalName("EntityType");

            var fkPattern = new Regex("FK_(?<parent>[^_]*)_(?<child>[^_]*)$");

            foreach (var entity in entities)
            {
                var props = entity.FindByLocalName("NavigationProperty").ToList();
                foreach (var prop in props)
                {
                    var relationship = prop.Attribute("Relationship").Value;
                    var fromRole = prop.Attribute("FromRole").Value;
                    var toRole = prop.Attribute("ToRole").Value;
                    var name = prop.Attribute("Name").Value;

                    var fkMatch = fkPattern.Match(relationship);
                    if (!fkMatch.Success)
                    {
                        continue;
                    }

                    var parent = fkMatch.Groups["parent"].Value;
                    var child = fkMatch.Groups["child"].Value;
                    string newName;
                    if (parent == fromRole)
                    {
                        newName = child;
                    }
                    else
                    {
                        newName = parent;
                    }
                    if (newName != name)
                    {
                        prop.SetAttributeValue("Name", newName);
                    }
                }
            }

            Console.WriteLine("Writing result to {0}", InputFileName);
            if (File.Exists(InputFileName))
            {
                File.Delete(InputFileName);
            }
            doc.Save(InputFileName);
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
