using System;
using System.Collections.Generic;
using System.IO;

namespace load_file
{
    /// <summary>
    /// Defines an IDisposable reader over a CSV. Uses the DelimitedLineParser class to actually parse the CSV
    /// </summary>
    class CSVReader : FileReader
    {
        /// <summary>
        /// The DelimitedLineParser that the class uses to parse each line read from the source file
        /// </summary>
        private DelimitedLineParser Parser;

        /// <summary>
        /// Constructs an instance with the specified configuration parameters
        /// </summary>
        /// <param name="SrcFile">The source file to read</param>
        /// <param name="RemoveEmbeddedTabs">True to replace tabs embedded in fields with four spaces</param>

        public CSVReader(string SrcFile, bool RemoveEmbeddedTabs) : base()
        {
            Rdr = new StreamReader(SrcFile);
            Parser = new DelimitedLineParser(Cfg.Delimiter, Cfg.SimpleParse);
            this.RemoveEmbeddedTabs = RemoveEmbeddedTabs;
        }

        /// <summary>
        /// Reads a line from the instance CSV
        /// </summary>
        /// <returns>The fields in a List, ordered left-to-right in the order parsed, or null if: a) there is no more data, or
        /// b) the EOF string was encountered, or c) MaxRows were read
        /// </returns>
        public override List<string> ReadLine()
        {
            string InputLine;
            List<string> Fields = null;

            while ((InputLine = NextLine()) != null)
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
                Fields = Parser.SplitLine(InputLine, RemoveEmbeddedTabs);
                break;
            }
            return Fields;
        }

        /// <summary>
        /// Gets the next line from the input file. Handles cases where a field
        /// contains an embedded CR by reading the next line (in that case) and appending
        /// it to the current line. Note - in this case, the physical lines read won't
        /// match the lines read reported by the caller...
        /// </summary>
        /// <returns>a line from the file</returns>
        private string NextLine()
        {
            string InputLine = string.Empty;
            do
            {
                string ThisLine = Rdr.ReadLine();
                if (ThisLine == null)
                {
                    break;
                }
                else
                {
                    InputLine += ThisLine;
                }
            } while (HasLineBreakInQuotedField(InputLine));
            return InputLine == string.Empty ? null : InputLine;
        }

        /// <summary>
        /// Checks to see if the passed string contains an odd number of quotes. If it does,
        /// then infers that the line contains a carriage return embedded in a quoted field.
        /// </summary>
        /// <param name="InputLine">the line to check</param>
        /// <returns>true if the line contains an odd number of quotes (doulbe-quotes)</returns>
        private bool HasLineBreakInQuotedField(string InputLine)
        {
            return InputLine.CountOf('"') % 2 != 0;
        }
    }
}
