using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmoteParser
{
    public class Emote : IComparable
    {
        public string Name
        {
            get;
            private set;
        }

        public EmoteSource Source
        {
            get;
            private set;
        }

        public Emote(string name, EmoteSource source)
        {
            Name = name;
            Source = source;
        }

        /// <summary>
        /// Creates an emote with a literal source of the same name.
        /// </summary>
        public Emote(string name)
        {
            Name = name;
            Source = new EmoteSource(string.Format("/{0}", name));
        }

        public override bool Equals(object obj)
        {
            Emote e = obj as Emote;
            if (e != null)
            {
                //If the given object is an emote, just compare the name strings.
                return Name.Equals(e.Name);
            }
            else
            {
                string s = obj as string;
                if (s != null)
                {
                    //If the given object is a string, just compare it to our name string.
                    return Name.Equals(s);
                }
            }

            //If all else failed, use the base comparison, which will most likely return false.
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            //We really only care about the name of the emote when doing comparisons,
            // so just return the hash code of the name.
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", Source, Name);
        }

        public int CompareTo(object obj)
        {
            string compareWith = string.Empty;

            //If the given object was an Emote, compare against its Name field.
            Emote e = obj as Emote;
            if (e != null)
            {
                compareWith = e.Name;
            }
            else if (obj != null)
            {
                //The given object wasn't an Emote. Just compare against its ToString.
                compareWith = obj.ToString();
            }

            //Return the result of comparing this Emote's Name field with the other object.
            return this.Name.CompareTo(compareWith);
        }
    }
}
