using System;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Icc;

namespace ExifExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Exif Extractor";

            if (args.Length == 0)
            {
                PrintUsage();
            }
            else
            {
                var includeIccProfile = args.Contains("--include-icc-profile");

                foreach (var filePath in args.Where(arg => arg != "--include-icc-profile"))
                {
                    if (!File.Exists(filePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"File not found: {filePath}");
                        Console.ResetColor();
                    }
                    else
                    {
                        try
                        {
                            var directories = ImageMetadataReader.ReadMetadata(filePath).OrderBy(d => d.Name);
                            var clickableFilePath = Path.GetFullPath(filePath);
                            Console.Write("\nMetadata for ");
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"\u001b]8;;file://{clickableFilePath}\u001b\\{filePath}\u001b]8;;\u001b\\");
                            Console.ResetColor();
                            
                            foreach (var directory in directories)
                            {
                                // Skip the ICC Profile directory if the flag is not set
                                if (!includeIccProfile && directory is IccDirectory)
                                {
                                    continue;
                                }

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"\n{directory.Name}:");
                                Console.ResetColor();
                                
                                foreach (var tag in directory.Tags.OrderBy(t => t.Name))
                                {
                                    Console.WriteLine($"  {tag.Name} = {tag.Description}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"An error occurred while reading the metadata for {filePath}:");
                            Console.WriteLine(ex.Message);
                            Console.ResetColor();
                        }
                    }
                }

                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        static void PrintUsage()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Usage: .\\ExifExtractor.exe [--include-icc-profile] <image file path> [<image file path> ...]");
            Console.WriteLine("You can provide one or more image file paths. Include the --include-icc-profile flag to include the ICC Profile in the output.");
            Console.ResetColor();
        }
    }
}
