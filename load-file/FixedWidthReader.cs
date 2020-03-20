using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace load_file
{
    /// <summary>
    /// Supports the ability to read ragged fixed width files containing tabs. In other words, the individual
    /// fields are fixed width, but trailing fields can be omitted.
    /// </summary>

    class FixedWidthReader : FileReader
    {
        /// <summary>
        /// Each element is the starting position of a field in the file, in left-to-right order. The
        /// Set is initialized with an extra field to simplify parsing. So it always contains one more
        /// element than the number of fields in the input file.
        /// </summary>

        private SortedSet<int> FieldStarts = new SortedSet<int>();

        /// <summary>
        /// Supports tab replacement
        /// </summary>

        private string TabReplacementString;

        /// <summary>
        /// Used to determine the format of the file from the data. Requires that the fields in the file
        /// be separated by at least one space/
        /// </summary>

        private const string StartOfFieldPattern = "\\s{1}[^\\s]"; // one whitespace followed by a non-whitespace

        /// <summary>
        /// If the instance is initialized by computing the file format from the data, then this field will
        /// be set to false if the file format is inconsistent. E.g.:
        ///   AAAA BBBBB CCCCCCCC DDD
        ///   AA BBBBB CCCCCCCC DDD
        ///   AAA BBBBB CCCCCCCC DDD
        /// If the instance is initialized with provided widths then there is no way for the class to determine
        /// whether the format is valid so it sets IsParseable to true.
        /// </summary>

        public bool IsParseable { get; private set; } = false;

        /// <summary>
        /// Initializes an instance with no reader. Used to intantiate the class to parse a line as a utility method
        /// </summary>

        public FixedWidthReader() : base()
        {
            TabReplacementString = new string(' ', Cfg.TabSize);
            InitFieldStartsFromLengths(ToIntArray(Cfg.Fixed));
            IsParseable = true;
        }

        /// <summary>
        /// Initializes an instance with field widths as provided in configuration settings. The reader will assume
        /// the file is parseable according to the provided format.
        /// </summary>
        /// <param name="PathSpec">File to read</param>

        public FixedWidthReader(string PathSpec) : base()
        {
            TabReplacementString = new string(' ', Cfg.TabSize);
            InitFieldStartsFromLengths(ToIntArray(Cfg.Fixed));
            Rdr = new StreamReader(PathSpec);
            IsParseable = true;
        }

        /// <summary>
        /// Initializes an instance, but computes the file format automatically. Sets the "IsParseable" field
        /// to false if the file does not exhibit a consistent format. This only works for files that have a space
        /// separator between fields. I.e. it can parse a file like "FIELD1 FIELD2 FIELD3" but it cannot parse a file
        /// like "FIELD1FIELD2FIELD3".
        /// </summary>
        /// <param name="PathSpec">File to read</param>
        /// <param name="tabSize">If tabs are encountered in the file, replace them with this many spaces</param>

        public FixedWidthReader(string PathSpec, int tabSize)
        {
            TabReplacementString = new string(' ', tabSize);
            DetermineFieldStartsFromFile(PathSpec);
            if (!(IsParseable = ValidateFormatConsistency(PathSpec)))
            {
                return;
            }
            Rdr = new StreamReader(PathSpec);
        }

        /// <summary>
        /// Reads a line from the file, splits it according to the file format, and returns the fields as a List of strings
        /// </summary>
        /// <exception cref="InvalidOperationException">if the file does not have a consistent format</exception>
        /// <returns>the fields as a List of strings</returns>

        public override List<string> ReadLine()
        {
            if (!IsParseable)
            {
                throw new InvalidOperationException("Attempt to read from a file that does not exhibit a consistent format");
            }
            string InputLine;
            List<string> Fields = null;
            while ((InputLine = Rdr.ReadLine()) != null)
            {
                ++TotLinesRead;
                TotBytesRead += InputLine.Length + 2; // CRLF
                if (SkipLines != 0)
                {
                    if (++LinesSkippedSoFar <= SkipLines)
                    {
                        continue;
                    }
                }
                if (RowsProcessed++ >= MaxRows)
                {
                    break;
                }
                if (HasEOFStr && InputLine.StartsWith(EOFStr))
                {
                    break;
                }
                Fields = ParseLine(InputLine.Replace("\t", TabReplacementString).TrimEnd());
                break;
            }
            return Fields;
        }

        /// <summary>
        /// Converts the passed List of strings having integer values into an array of int
        /// </summary>
        /// <param name="StringList"></param>
        /// <returns>an integer array transcribed from the passed string list</returns>

        private static int[] ToIntArray(List<string> StringList)
        {
            int[] Items = new int[StringList.Count];
            int Idx = 0;
            foreach (string s in StringList)
            {
                Items[Idx++] = int.Parse(s);
            }
            return Items;
        }

        /// <summary>
        /// Initializes the file format from the passed list of field lengths
        /// </summary>
        /// <param name="FieldLengths">A list of field lengths</param>

        private void InitFieldStartsFromLengths(int[] FieldLengths)
        {
            int Cumulative = 0;
            foreach (int FieldLength in FieldLengths)
            {
                FieldStarts.Add(Cumulative);
                Cumulative += FieldLength;
            }
            FieldStarts.Add(int.MaxValue); // simplifies the parsing later on
        }

        /// <summary>
        /// Parses the passed line into a List of fields using the instance format definition
        /// </summary>
        /// <param name="InLine">The line to parse. Should not contain tabs or will produce incorrect results</param>
        /// <returns>A List of fields. Each field is trimmed</returns>

        public List<string> ParseLine(string InLine)
        {
            List<string> Fields = new List<string>();
            int PriorFieldStart = 0;
            foreach (int ThisFieldStart in FieldStarts)
            {
                if (ThisFieldStart == 0) // inserted by the class so - first start position is always zero
                {
                    continue;
                }
                if (InLine.Length < ThisFieldStart - 1)
                {
                    // line is shorter than the field width so get whatever is available
                    Fields.Add(InLine.Substring(PriorFieldStart).Trim());
                    break;
                }
                // fall through means a full sized field
                Fields.Add(InLine.Substring(PriorFieldStart, ThisFieldStart - PriorFieldStart).Trim());
                PriorFieldStart = ThisFieldStart;
            }
            while (Fields.Count < FieldStarts.Count - 1)
            {
                Fields.Add(string.Empty); // supports ragged files with one or more missing fields at the end of the record
            }
            return Fields;
        }

        /// <summary>
        /// Reads the passed file to verify that each row adheres to the format defined by the
        /// FieldStarts Set -- assuming that the file has at least one space separating each field.
        /// </summary>
        /// <param name="PathSpec">The file to validate</param>
        /// <returns>True if the file format is consistent, else false</returns>

        private bool ValidateFormatConsistency(string PathSpec)
        {
            using (StreamReader Rdr = new StreamReader(PathSpec))
            {
                string Inline;
                while ((Inline = Rdr.ReadLine()) != null)
                {
                    Inline = Inline.Replace("\t", TabReplacementString);
                    foreach (int FieldStart in FieldStarts)
                    {
                        if (FieldStart == 0)
                        {
                            continue;
                        }
                        if (Inline.Length < FieldStart - 1)
                        {
                            break; // supports ragged files
                        }
                        if (Inline[FieldStart-1] != ' ')
                        {
                            return false; // no space where format says space should be
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Scans the passed file and determines the format from the data -- assuming that fields are separated by
        /// at least one space. (Refer to the constructor for additional information). Initializes the "FieldStarts"
        /// field.
        /// </summary>
        /// <param name="PathSpec">The file to analyze</param>

        private void DetermineFieldStartsFromFile(string PathSpec)
        {
            FieldStarts.Add(0); // first field is always at position zero
            using (StreamReader Rdr = new StreamReader(PathSpec))
            {
                string Inline;
                while ((Inline = Rdr.ReadLine()) != null)
                {
                    foreach (Match m in Regex.Matches(Inline.Replace("\t", TabReplacementString), StartOfFieldPattern))
                    {
                        FieldStarts.Add(m.Index + 1);
                    }
                }
            }
            FieldStarts.Add(int.MaxValue); // simplifies the parsing later on
        }
    }
}
