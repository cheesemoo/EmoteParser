using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.IO;
using System.IO.Compression;

namespace EmoteParser
{
    public class EmoteSource
    {
        private static DateTime fetchNextSubAt = DateTime.Now;
        private const uint FETCH_DELAY_IN_SECONDS = 2;

        public string Origin
        {
            get;
            private set;
        }

        public bool IsFile
        {
            get
            {
                //If it's not a literal or a subreddit, it must be a file.
                return (!IsLiteral && !IsSubreddit);
            }
        }

        public bool IsSubreddit
        {
            get
            {
                //If it starts with "/r/", it's a subreddit.
                return Origin.StartsWith("/r/");
            }
        }

        public bool IsLiteral
        {
            get
            {
                //Literal sources start with a slash, but aren't subreddits.
                return Origin.StartsWith("/") && !IsSubreddit;
            }
        }

        public string Filename
        {
            get
            {
                if (IsFile)
                {
                    return Path.GetFileName(Origin);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        List<Emote> _emotes = new List<Emote>();
        public List<Emote> Emotes
        {
            get
            {
                return _emotes;
            }
        }

        public EmoteSource(string origin)
        {
            Origin = origin.Trim();
        }

        /// <summary>
        /// Given a string of CSS, extracts the targets of any href selectors and returns
        /// them all in a list.
        /// </summary>
        private static List<string> PullEmoteNames(string css)
        {
            //First, ignore CSS comments.
            css = StripComments(css);

            //Then, ignore anything that's been explicitly excluded from the ponyscript.
            //We probably don't want to be loading it here either.
            css = StripIgnoreStuff(css);

            //Extract all href targets via regex. The \\? bits are in there because hardcoded CSS files (MRP, etc.)
            // may have the quotes escaped with a backslash.
            string regexString = @"a\[href[\^|]?=\\?[""']/([A-Za-z0-9]+)\\?[""']\]";
            Regex regex = new Regex(regexString);
            var matches = regex.Matches(css);

            //Add all captured values to the output list
            var output = new List<string>();
            foreach (Match m in matches)
            {
                output.Add(m.Groups[1].Value);
            }

            return output;
        }

        private static string StripIgnoreStuff(string noComments)
        {
            string regexString = @"START-PONYSCRIPT-IGNORE[^{]*{[^}]*}.*END-PONYSCRIPT-IGNORE[^{]*{[^}]*}";
            return Regex.Replace(noComments, regexString, "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        /// <summary>
        /// Adapted from: http://james.padolsey.com/javascript/removing-comments-in-javascript/
        /// </summary>
        private static string StripComments(string css)
        {
            bool singleQuote = false;
            bool doubleQuote = false;
            bool blockComment = false;
            char dummy = '_';

            //Iterate over the character array, stripping comments as we go
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < css.Length; i++)
            {
                //Get the current, next, and previous character. Any indices outside the 
                // actual string should result in a dummy character.
                char c = css[i];
                char prev = (i > 0) ? css[i - 1] : dummy;
                char next = (i < css.Length - 1) ? css[i + 1] : dummy;

                if (singleQuote)
                {
                    //We're currently in a single-quoted string. If this character is a quote
                    // and the previous character isn't a slash (escaping this character), we
                    // just ended the string. Exit single quote mode.
                    if (c == '\'' && prev != '\\')
                    {
                        singleQuote = false;
                    }

                    //Regardless, append this character and move on to the next.
                    str.Append(c);
                    continue;
                }

                if (doubleQuote)
                {
                    //We're currently in a double-quoted string. If this character is a quote
                    // and the previous character isn't a slash (escaping this character), we
                    // just ended the string. Exit double quote mode.
                    if (c == '"' && prev != '\\')
                    {
                        doubleQuote = false;
                    }

                    //Regardless, append this character and move on to the next.
                    str.Append(c);
                    continue;
                }

                if (blockComment)
                {
                    //We're currently in a block comment. If the current and next characters end the
                    // comment block, skip an extra character ahead, and exit comment mode.
                    if (c == '*' && next == '/')
                    {
                        i++;
                        blockComment = false;
                    }

                    //Do not append this character, just continue.
                    continue;
                }

                doubleQuote = (c == '"');
                singleQuote = (c == '\'');

                //If we're just starting a block comment, enter block comment mode.
                if (c == '/' && next == '*')
                {
                    blockComment = true;

                    //Do not append this character, just continue.
                    continue;
                }

                //If we got down this far, the current character needs appending.
                str.Append(c);
            }

            return str.ToString();
        }

        /// <summary>
        /// Handles loading emotes from this source.
        /// The source should be either the name of a subreddit, starting with /r/, a path
        /// to a file with CSS in it, or a literal emote name, starting with a slash.
        /// </summary>
        public void Load()
        {
            //If the origin is a literal emote, we can just leave it as-is.
            if (IsLiteral)
            {
                //Remove the initial slash and add to the list of emotes.
                string name = Origin.Substring(1);
                _emotes.Add(new Emote(name, this));

                Console.WriteLine("Loaded literal emote: {0}", name);
            }
            else if (IsSubreddit)
            {
                //Might need to wait before fetching another sub, to satisfy reddit's guidelines
                while (DateTime.Now < fetchNextSubAt)
                {
                    Thread.Sleep(50);
                }

                //Can't fetch the next stylesheet until 2 seconds from this request.
                fetchNextSubAt = DateTime.Now.AddSeconds(FETCH_DELAY_IN_SECONDS);

                Console.Write("Loading emotes from subreddit: {0}.", Origin);
                LoadEmotesFromSubreddit(Origin);
                Console.WriteLine(" Count: {0}", _emotes.Count);
            }
            else if (IsFile)
            {
                Console.Write("Loading emotes from file: {0}.", Filename);
                LoadEmotesFromFile(Origin);
                Console.WriteLine(" Count: {0}", _emotes.Count);
            }
        }

        /// <summary>
        /// Adds any emotes in the given list that don't already exist for this source
        /// to the list of emotes for this source.
        /// </summary>
        private void AddEmotes(string cssString)
        {
            var hrefs = PullEmoteNames(cssString);

            foreach (string s in hrefs)
            {
                if (!Emotes.Any(e => e.Equals(s)))
                {
                    Emotes.Add(new Emote(s, this));
                }
            }
        }

        private void LoadEmotesFromFile(string filePath)
        {
            //Load all the text in the file, then pull the emotes from it
            string cssString = File.ReadAllText(filePath);
            AddEmotes(cssString);
        }

        private void LoadEmotesFromSubreddit(string subreddit)
        {
            //First fetch the CSS for this subreddit, then pull the emotes from it
            string cssString = DownloadCss(subreddit);
            AddEmotes(cssString);
        }

        /// <summary>
        /// Handles downloading the CSS for the given subreddit. Tries to disable all possible caching
        /// options so that the result is always fresh.
        /// </summary>
        private static string DownloadCss(string subreddit)
        {
            using (WebClient client = new WebClient())
            {
                //Set up the client to advertise gzip/deflate support and disable client-side caching.
                client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);
                client.Headers[HttpRequestHeader.Accept] = "text/css";
                client.Headers[HttpRequestHeader.AcceptLanguage] = "en-US";
                client.Headers[HttpRequestHeader.AcceptEncoding] = "gzip, deflate";

                //Fetch the CSS data, probably compressed. Use a random query string to try to ensure
                // that the CSS we receive isn't being cached anywhere.
                client.QueryString.Add("randomJunk", Path.GetRandomFileName());
                string cssUri = string.Format("http://www.reddit.com{0}/stylesheet.css", subreddit);
                byte[] data = client.DownloadData(cssUri);

                //Decompress the CSS data and return the decoded string.
                return DecodeData(data, client.ResponseHeaders);
            }
        }

        /// <summary>
        /// Uses the information in the response headers to decode the received bytes to a string.
        /// </summary>
        private static string DecodeData(byte[] data, WebHeaderCollection responseHeaders)
        {
            //If the content type is not text/css, something's probably wrong.
            if (responseHeaders[HttpResponseHeader.ContentType] != "text/css")
            {
                throw new ArgumentException("Response header content type should be text/css");
            }

            //Ditto if ContentLength doesn't match the size of the data array.
            if (data.Length != int.Parse(responseHeaders[HttpResponseHeader.ContentLength]))
            {
                throw new ArgumentException("Data size not equal to ContentLength");
            }

            //Choose the correct decompressor.
            Stream decompressStream;
            switch (responseHeaders[HttpResponseHeader.ContentEncoding])
            {
                case "gzip":
                    decompressStream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
                    break;

                case "deflate":
                    decompressStream = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
                    break;

                default:
                    throw new ArgumentException("Encoding is neither gzip nor deflate.");
            }

            //Finally, uncompress the data and get a readable string.
            StreamReader reader = new StreamReader(decompressStream);
            string str = reader.ReadToEnd();
            reader.Close();

            return str;
        }

        public override string ToString()
        {
            return Origin;
        }
    }
}
