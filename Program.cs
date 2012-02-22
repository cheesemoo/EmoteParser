using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.IO.Compression;

namespace EmoteParser
{
    static class Program
    {
        static List<Emote> _emotes = new List<Emote>();
        static List<Emote> _uniqueEmotes = new List<Emote>();

        static void Main(string[] args)
        {
            string inputFolder;
            string outputFolder;

            if (args.Length == 0)
            {
                //Use the current directory for both folders if no command line args are given.
                inputFolder = outputFolder = ".";
            }
            else if(args.Length == 1)
            {
                //Use the given directory for both folders if only one arg is given.
                inputFolder = outputFolder = args[0];
            }
            else if (args.Length == 2)
            {
                //Use the given input and output directories if 2 args are given.
                inputFolder = args[0];
                outputFolder = args[1];
            }
            else
            {
                throw new ArgumentException("Too many arguments!");
            }

            string sourceFile = Path.Combine(inputFolder, "sources.txt");
            string allEmotesFile = Path.Combine(outputFolder, "allEmotes.txt");
            string conflictsFile = Path.Combine(outputFolder, "conflicts.txt");

            //Load emotes from all sources
            var sources = File.ReadAllText(sourceFile).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var emoteSources = new List<EmoteSource>();
            foreach (string s in sources)
            {
                var source = new EmoteSource(s);
                source.Load();
                emoteSources.Add(source);
                _emotes.AddRange(source.Emotes);
                AddUniquesToList(source.Emotes);
            }

            //Write the 'all emotes' file
            WriteAll(allEmotesFile);

            //Write the 'conflicts' file
            WriteConflicts(conflictsFile);
        }

        private static void WriteConflicts(string conflictsFile)
        {
            //Build a list of lists of duplicates
            List<List<Emote>> duplicates = new List<List<Emote>>();
            foreach (Emote emote in _uniqueEmotes)
            {
                //Find all emotes sharing this name
                var list = _emotes.FindAll(e => e.Name == emote.Name);
                if (list.Count > 1)
                {
                    //If there are duplicate sources for this emote, save the list.
                    duplicates.Add(list);
                }
            }

            //Write the duplicates to file.
            using (var stream = File.CreateText(conflictsFile))
            {
                foreach (var list in duplicates)
                {
                    stream.Write(list[0].Name + ": ");
                    string sources = string.Join(", ", list.Select(e => e.Source));
                    stream.WriteLine(sources);
                }
            }
        }

        private static void WriteAll(string allEmotesFile)
        {
            using (var stream = File.CreateText(allEmotesFile))
            {
                foreach (var emote in _emotes)
                {
                    stream.WriteLine(emote.ToString());
                }
            }
        }

        private static void AddUniquesToList(List<Emote> emotes)
        {
            foreach (Emote e in emotes)
            {
                if (!_uniqueEmotes.Contains(e))
                {
                    _uniqueEmotes.Add(e);
                }
            }
        }
    }
}
