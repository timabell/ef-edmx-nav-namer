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
            CommandLineParser.CommandLineParser parser = CreateParser();

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

            p.CreateDocumentation();
        }

        private static CommandLineParser.CommandLineParser CreateParser()
        {
            CommandLineParser.CommandLineParser parser = new CommandLineParser.CommandLineParser();
            ValueArgument<SqlConnectionStringBuilder> connectionStringArgument = new ValueArgument<SqlConnectionStringBuilder>('c', "connectionString", "ConnectionString of the documented database")
            {
                Optional = false,
                ConvertValueHandler = (stringValue) =>
                {
                    SqlConnectionStringBuilder connectionStringBuilder;
                    try
                    {
                        connectionStringBuilder = new SqlConnectionStringBuilder(stringValue);
                    }
                    catch
                    {
                        throw new InvalidConversionException("invalid connection string", "connectionString");
                    }
                    if (String.IsNullOrEmpty(connectionStringBuilder.InitialCatalog))
                    {
                        throw new InvalidConversionException("no InitialCatalog was specified", "connectionString");
                    }

                    return connectionStringBuilder;
                }
            };
            FileArgument inputFileArgument = new FileArgument('i', "input", "original edmx file") { FileMustExist = true, Optional = false };
            FileArgument outputFileArgument = new FileArgument('o', "output", "output edmx file - Default : original edmx file") { FileMustExist = false, Optional = true };

            parser.Arguments.Add(connectionStringArgument);
            parser.Arguments.Add(inputFileArgument);
            parser.Arguments.Add(outputFileArgument);

            parser.IgnoreCase = true;
            return parser;
        }

        public String InputFileName { get; set; }

        public Program(String inputFileName)
        {
            InputFileName = inputFileName;
        }

        private void CreateDocumentation()
        {
            XDocument doc = XDocument.Load(InputFileName);

            if (doc.Root == null)
            {
                throw new Exception(string.Format("Loaded XDocument Root is null. File: {0}", InputFileName));
            }
            var entityTypeElements = doc.FindByLocalName("EntityType");

            int i = 0;
            foreach (XElement entityTypeElement in entityTypeElements)
            {
                String tableName = entityTypeElement.Attribute("Name").Value;
                var propertyElements = entityTypeElement.FindByLocalName("Property");

                Console.Clear();
                Console.WriteLine("Analyzing table {0} of {1}", i++, entityTypeElements.Count());
                Console.WriteLine(" => TableName : {0}" +
                                  "\n => property count : {1}", tableName, propertyElements.Count());

                //AddNodeDocumentation(entityTypeElement, GetTableDocumentation(tableName));

                foreach (XElement propertyElement in propertyElements)
                {
                    String columnName = propertyElement.Attribute("Name").Value;
                    //AddNodeDocumentation(propertyElement, GetColumnDocumentation(tableName, columnName));
                }
            }

            Console.WriteLine("Writing result to {0}", InputFileName);
            if (File.Exists(InputFileName))
                File.Delete(InputFileName);
            doc.Save(InputFileName);
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
