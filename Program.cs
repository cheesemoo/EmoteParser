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
            string rootFolder;
            if (args.Length == 0)
            {
                //Use the current directory for the root folder if no command line args are given.
                rootFolder = ".";
            }
            else
            {
                rootFolder = args[0];
            }

            string sourceFile = Path.Combine(rootFolder, "sources.txt");
            string allEmotesFile = Path.Combine(rootFolder, "allEmotes.txt");
            string conflictsFile = Path.Combine(rootFolder, "conflicts.txt");

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
