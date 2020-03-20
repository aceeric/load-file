using System;
using System.Collections.Generic;
using System.IO;

namespace load_file
{
    /// <summary>
    /// Defines an interface definition for a file reader that can read fixed field length files, or
    /// delimited files.
    /// </summary>

    abstract class FileReader : IDisposable
    {
        /// <summary>
        /// The number of bytes read by the instance. (Excludes CR/LF)
        /// </summary>
        public long TotBytesRead { get; protected set; }

        /// <summary>
        /// Releases resources held by the instance
        /// </summary>
        public void Dispose()
        {
            if (Rdr != null)
            {
                Rdr.Dispose();
                Rdr = null;
            }
        }

        /// <summary>
        /// Reads a line from the input file
        /// </summary>
        /// <returns></returns>
        public abstract List<string> ReadLine();

        /// <summary>
        /// Reader for the class
        /// </summary>
        protected StreamReader Rdr;

        /// <summary>
        /// The number of lines to skip at the head of the file
        /// </summary>
        protected int SkipLines;

        /// <summary>
        /// The maximum number of rows (excluding SkipLines) to read from the source file
        /// </summary>
        protected int MaxRows;

        /// <summary>
        /// The total number of lines read by the reader (including skipped lines)
        /// </summary>
        protected int TotLinesRead;

        /// <summary>
        /// The EOF string to terminate reading
        /// </summary>
        protected string EOFStr;

        /// <summary>
        /// True if EOFStr is not null or empty
        /// </summary>
        protected bool HasEOFStr;

        /// <summary>
        /// True to replace tabs embedded in fields with four spaces
        /// </summary>
        protected bool RemoveEmbeddedTabs;

        /// <summary>
        /// Rows skipped so far
        /// </summary>
        protected int LinesSkippedSoFar;

        /// <summary>
        /// Rows processed so far (excludes skipped rows)
        /// </summary>
        protected int RowsProcessed;

        /// <summary>
        /// Initializes defaults for derived classes
        /// </summary>

        protected FileReader()
        {
            SkipLines = Math.Max(Cfg.SkipLines, Cfg.HeaderLine);
            MaxRows = Cfg.MaxRows;
            EOFStr = Cfg.EOFStr;
            HasEOFStr = !string.IsNullOrEmpty(Cfg.EOFStr);
            LinesSkippedSoFar = 0;
            RowsProcessed = 0;
        }

        /// <summary>
        /// Factory method to instantiate a reader based on configuration settings
        /// </summary>
        /// <param name="SrcFile">The source file the load</param>
        /// <param name="RemoveEmbeddedTabs">True to remove tabs embedded within fields</param>
        /// <returns>A FileReader instance, based on configuration settings</returns>

        public static FileReader NewFileReader(string SrcFile, bool RemoveEmbeddedTabs)
        {
            if (Cfg.Fixed.IsNullOrEmpty())
            {
                // then created a CSV Parser
                return new CSVReader(SrcFile, RemoveEmbeddedTabs);
            }
            else
            {
                // create a fixed file reader (always replaces tabs with chars)
                return new FixedWidthReader(SrcFile);
            }
        }
    }
}
