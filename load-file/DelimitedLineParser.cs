using System.Collections.Generic;
using System.IO;

namespace load_file
{
    /// <summary>
    /// Provides functionality to parse a line of text and break it into fields
    /// </summary>

    class DelimitedLineParser
    {
        /// <summary>
        /// A class reference to the line being parsed
        /// </summary>

        private string Line;

        /// <summary>
        /// Current parse position within the Line instance
        /// </summary>
        private int Pos;

        /// <summary>
        /// Delimiter to parse on (initialized by Constructor)
        /// </summary>
        private char Delimiter;

        /// <summary>
        /// True to simply split the input line on the delimiter and not attempt to handle quoted fields
        /// with embedded delimiters
        /// </summary>
        private bool SimpleParse;

        /// <summary>
        /// End of line character
        /// </summary>
        private const char EOL = '\0';

        /// <summary>
        /// The double quote character
        /// </summary>
        private const char DBLQUOTE = '"';

        /// <summary>
        /// The comma character
        /// </summary>
        private const char COMMA = ',';

        /// <summary>
        /// The states that the line parser can be in
        /// </summary>
        enum FieldState
        {
            /// <summary>
            /// Inside a quoted field
            /// </summary>
            IN_QUOTED_FIELD,
            /// <summary>
            /// Inside an un-quoted field
            /// </summary>
            IN_UNQUOTED_FIELD,
            /// <summary>
            /// Not in a field
            /// </summary>
            NOT_IN_FIELD
        };

        /// <summary>
        /// Constructs an instance with the passed initializers
        /// </summary>
        /// <param name="Delimiter">The delimiter to use</param>
        /// <param name="SimpleParse">If true, just to simple line parsing: splits input lines on the delimiter and does
        /// not attempt to handle embedded delimiters with quoted fields. Good for cases where a file is guaranteed not
        /// to contain embedded delimiters</param>

        public DelimitedLineParser(char Delimiter, bool SimpleParse)
        {
            this.Delimiter = Delimiter;
            this.SimpleParse = SimpleParse;
        }

        /// <summary>
        /// Splits the passed Line, and returns it as a List of strings, with each element in the list in the
        /// same position as the corrresponding field in the passed line.
        /// </summary>
        /// <param name="LineToParse">The line to parse</param>
        /// <param name="RemoveEmbeddedTabs">True to replace tabs embedded within fields with four spaces</param>
        /// <returns>a List of strings. Each string is trimmed. Hence, can return string.Empty for fields</returns>

        public List<string> SplitLine(string LineToParse, bool RemoveEmbeddedTabs)
        {
            if (SimpleParse)
            {
                return new List<string>(Line.Split(Delimiter));
            }

            Line = LineToParse;
            Pos = 0;
            List<string> Fields = new List<string>();
            string Field = string.Empty;
            FieldState State = FieldState.NOT_IN_FIELD;

            do
            {
                char LineChar = ReadChar();
                if (LineChar == EOL)
                {
                    Fields.Add(Field);
                    return Fields;
                }
                if (LineChar == DBLQUOTE)
                {
                    if (State == FieldState.NOT_IN_FIELD)
                    {
                        State = FieldState.IN_QUOTED_FIELD;
                        continue;
                    }
                    else // in a field
                    {
                        if (!AtEOL())
                        {
                            if (State == FieldState.IN_QUOTED_FIELD && PeekChar() == DBLQUOTE) // then two quotes in a row is a quoted quote
                            {
                                ReadChar(); // throw the second quote away
                                Field += DBLQUOTE;
                                continue;
                            }
                            else if (State == FieldState.IN_QUOTED_FIELD) // end of field
                            {
                                Fields.Add(Field);
                                State = FieldState.NOT_IN_FIELD;
                                Field = string.Empty;
                                if (!ReadAheadToNextDelimiter())
                                {
                                    return Fields;
                                }
                                continue;
                            }
                            else // just a quote in a non-quoted data field
                            {
                                Field += LineChar;
                                continue;
                            }
                        }
                        else // end of line
                        {
                            Fields.Add(Field);
                            return Fields;
                        }
                    }
                }
                else
                {
                    if (LineChar == Delimiter)
                    {
                        if (State == FieldState.IN_QUOTED_FIELD)
                        {
                            if (LineChar == '\t' && RemoveEmbeddedTabs)
                            {
                                Field += "    "; // four spaces
                            }
                            else
                            {
                                Field += LineChar;
                            }
                            continue;
                        }
                        State = FieldState.NOT_IN_FIELD;
                        Fields.Add(Field);
                        Field = string.Empty;
                        continue;
                    }
                    else // this is not a delimiter
                    {
                        if (State == FieldState.NOT_IN_FIELD)
                        {
                            State = FieldState.IN_UNQUOTED_FIELD;
                        }
                        Field += LineChar;
                    }
                }
            } while (true);
        }

        /// <summary>
        /// Check to see if the current line position is at the end of the line
        /// </summary>
        /// <returns>True if at the end of the line</returns>
        private bool AtEOL()
        {
            return Pos >= Line.Length;
        }

        /// <summary>
        /// Read one character from the input line at the current position and advance the position counter
        /// </summary>
        /// <returns>The character read</returns>

        private char ReadChar()
        {
            char c = PeekChar();
            ++Pos;
            return c;
        }

        /// <summary>
        /// Reads the current character from the line based on line position, but does not
        /// advance the position counter
        /// </summary>
        /// <returns>the character or EOL if at end of line</returns>

        private char PeekChar()
        {
            return Pos < Line.Length ? Line[Pos] : EOL;
        }

        /// <summary>
        /// Reads until a delimiter is encountered. Used to handle cases where a line might look
        /// like this: "field1"    ,     "field2"
        /// E.g. quote-enclosed fields with spaces between the quotes and the delimiters
        /// </summary>
        /// <returns>True if a delimiter was encoutered. False if EOL was encountered (no more delimiters)</returns>

        private bool ReadAheadToNextDelimiter()
        {
            char c;
            while ((c = ReadChar()) != EOL)
            {
                if (c == Delimiter)
                {
                    return true;
                }
            }
            // unable to locate delimiter
            return false;
        }

        /// <summary>
        /// Reads the first thousand rows of the passed file and tries to determine the delimiter from the data
        /// </summary>
        /// <param name="pathspec">The input file</param>
        /// <returns>The delimiter if it could be determined, else zero</returns>

        public static char CalcDelimiter(string pathspec)
        {
            const int MAX_ROWS_TO_READ = 1000;
            int i = 0;
            int[] tabs = new int[MAX_ROWS_TO_READ];
            int[] pipes = new int[MAX_ROWS_TO_READ];
            int[] commas = new int[MAX_ROWS_TO_READ];
            string line;
            using (StreamReader Reader = new StreamReader(pathspec))
            {
                while (i < MAX_ROWS_TO_READ && (line = Reader.ReadLine()) != null)
                {
                    tabs[i] = line.Split('\t').Length;
                    pipes[i] = line.Split('|').Length;
                    commas[i] = line.Split(',').Length;
                    ++i;
                }
            }
            if (IsLikely(tabs))
            {
                return '\t';
            }
            if (IsLikely(pipes))
            {
                return '|';
            }
            if (IsLikely(commas))
            {
                return ',';
            }
            return (char)0;
        }

        /// <summary>
        /// Examines the array of field counts created by splitting on a delimiter. If more than 95%
        /// of the lines have the same number of fields then the line was probably split by the correct
        /// delimiter
        /// </summary>
        /// <param name="Delim">An array of line lengths. E.g. line zero was split into 15 fields, line 1 was
        /// split into 15 fields, and so on...</param>
        /// <returns></returns>

        private static bool IsLikely(int[] Delim)
        {
            for (int i = 0; i < Delim.Length; ++i)
            {
                int cnt = 0;
                for (int j = 0; j < Delim.Length; ++j)
                {
                    if (Delim[j] > 1 && Delim[j] == Delim[i])
                    {
                        ++cnt;
                    }
                }
                if ((float)cnt / Delim.Length > .95)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
