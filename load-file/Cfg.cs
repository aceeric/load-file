using System.Collections.Generic;
using System.IO;
using static load_file.Globals;

namespace load_file
{
    /// <summary>
    /// A class to hold simplified representation of configurations from the command-line. In some cases, command line
    /// arg values are transformed to be closer to how they are used programmatically, whereas args on the command line are
    /// closer to how they are meaningful to the user. For example, the SQL timeout on the command line is specified in hours,
    /// whereas it is specified in this class as seconds.
    /// </summary>
    class Cfg
    {
        /// <summary>
        /// Specifies the pathname of the file to load
        /// </summary>
        public static string File;
        /// <summary>
        /// Specifies the pathname of the file to log error records to
        /// </summary>
        public static string ErrFile;
        /// <summary>
        /// Defines a SQL timeout in seconds
        /// </summary>
        public static int SQLTimeout;
        /// <summary>
        /// Specifies the pathname of the file to generate frequencies into
        /// </summary>
        public static string FreqFile;
        /// <summary>
        /// Specifies the target database to load the data into
        /// </summary>
        public static string Db;
        /// <summary>
        /// Specifies the server name
        /// </summary>
        public static string Server;
        /// <summary>
        /// Specifies the target table to load the data into in the form schema.tablename
        /// </summary>
        public static string Tbl;
        /// <summary>
        /// The schema of the target table
        /// </summary>
        public static string Schema;
        /// <summary>
        /// True if the user wants to profile the source data and ensure data type compatibility with the
        /// target table (if the target table already exists)
        /// </summary>
        public static bool Profile;
        /// <summary>
        /// Indicates that the target table should not be loaded
        /// </summary>
        public static bool NoLoad;
        /// <summary>
        /// True to display the table DDL to the console
        /// </summary>
        public static bool ShowDDL;
        /// <summary>
        /// True if file should be loaded via BCP instead of SqlBulkCopy
        /// </summary>
        public static bool BCP;
        /// <summary>
        /// True if file should be prepped prior to load
        /// </summary>
        public static bool Prep;
        /// <summary>
        /// A directory to hold prepped files. If not specified, then prepped files are placed in the same folder as the input file
        /// </summary>
        public static string PrepDir;
        /// <summary>
        /// True if prepped file should be retained after successful load. Otherwise deleted
        /// </summary>
        public static bool PrepRetain;
        /// <summary>
        /// Defines the pathname of a split columns file
        /// </summary>
        public static string Split;
        /// <summary>
        /// Defines a different split character. (The default is the colon character)
        /// </summary>
        public static string SplitStr;
        /// <summary>
        /// Indicates that if the target table does not exist - and is created by the utility - that
        /// the table is created with typed column names. Otherwise created with VARCHAR column names
        /// </summary>
        public static bool Typed;
        /// <summary>
        /// Indicates that if the target table - if it exists - should be truncated before loading
        /// </summary>
        public static bool Truncate;
        /// <summary>
        /// Indicates that if the target table - if it exists - should be dropped before loading
        /// </summary>
        public static bool Drop;
        /// <summary>
        /// Specifies the field delimiter to use
        /// </summary>
        public static char Delimiter;
        /// <summary>
        /// Defines field widths if the input file is fixed field
        /// </summary>
        public static List<string> Fixed;
        /// <summary>
        /// Specifies the number of characters that tabs are to be expanded to for fixed format files
        /// </summary>
        public static int TabSize;
        /// <summary>
        /// Specifies the max data rows in the source file to process
        /// </summary>
        public static int MaxRows;
        /// <summary>
        /// Specifies that the utility should display the first n lines and exit
        /// </summary>
        public static int Preview;
        /// <summary>
        /// If True, simply splits incoming fields on the defined delimiter (does not handle quote-enclosed fields)
        /// </summary>
        public static bool SimpleParse;
        /// <summary>
        /// Specifies the initial number of lines in the file to skip
        /// </summary>
        public static int SkipLines;
        /// <summary>
        /// Specifies the maximum number of errors to accommodate before aborting
        /// </summary>
        public static int MaxErrors;
        /// <summary>
        /// Specifies the one-relative row in the file that contains a header.
        /// </summary>
        public static int HeaderLine;
        /// <summary>
        /// Defines and EOF string which - if encountered - terminates file processing
        /// </summary>
        public static string EOFStr;
        /// <summary>
        /// Path of a file containing a list of column names for the source file (useful if the sourcefile
        /// does not have a header)
        /// </summary>
        public static string ColFile;

        const float SECONDS_PER_HOUR = 60 * 60;

        /// <summary>
        /// Pulls the settings out of the App Settings and packages them together with some cleanup.
        /// </summary>
        public static void Init()
        {
            File = NullIfEmpty(AppSettingsImpl.File.Value);
            ErrFile = NullIfEmpty(AppSettingsImpl.ErrFile.Value);

            // SQL timeout is defaulted to 1 (one hour) by the app settings and parsing is validated there as well
            // so it is safe to parse it here
            SQLTimeout = (int)(SECONDS_PER_HOUR * float.Parse(AppSettingsImpl.SQLTimeout.Value));

            FreqFile = NullIfEmpty(AppSettingsImpl.FreqFile.Value);
            Db = NullIfEmpty(AppSettingsImpl.Db.Value);
            Server = NullIfEmpty(AppSettingsImpl.Server.Value);

            if (AppSettingsImpl.Tbl.Initialized)
            {
                Tbl = MakeTableNameValid(AppSettingsImpl.Tbl.Value);
                Schema = ServerUtils.SchemaFromTableName(AppSettingsImpl.Tbl.Value);
            }
            else
            {
                Tbl = MakeTableNameValid(Path.GetFileNameWithoutExtension(File));
                Schema = "dbo";
            }

            Profile = AppSettingsImpl.Profile.Value;
            NoLoad = AppSettingsImpl.NoLoad.Value;
            ShowDDL = AppSettingsImpl.ShowDDL.Value;
            BCP = AppSettingsImpl.BCP.Value;
            Prep = AppSettingsImpl.Prep.Value;
            PrepDir = NullIfEmpty(AppSettingsImpl.PrepDir.Value);
            PrepRetain = AppSettingsImpl.PrepRetain.Value;
            Split = NullIfEmpty(AppSettingsImpl.Split.Value);
            SplitStr = NullIfEmpty(AppSettingsImpl.SplitStr.Value);
            Typed = AppSettingsImpl.Typed.Value;
            Truncate = AppSettingsImpl.Truncate.Value;
            Drop = AppSettingsImpl.Drop.Value;
            // delimiter is validated by settings so safe to just map it here
            Delimiter = AppSettingsImpl.Delimiter.Value.Xlat(new string[] { "pipe", "comma", "tab", "auto" }, new char[] {'|', ',', '\t', (char) 0 });
            Fixed = AppSettingsImpl.Fixed.Value;
            TabSize = AppSettingsImpl.TabSize.Value;
            MaxRows = AppSettingsImpl.MaxRows.Value;
            Preview = AppSettingsImpl.Preview.Value;
            SimpleParse = AppSettingsImpl.SimpleParse.Value;
            SkipLines = AppSettingsImpl.SkipLines.Value;
            MaxErrors = AppSettingsImpl.MaxErrors.Value;
            HeaderLine = AppSettingsImpl.HeaderLine.Value;
            EOFStr = NullIfEmpty(AppSettingsImpl.EOFStr.Value);
            ColFile = NullIfEmpty(AppSettingsImpl.ColFile.Value);

            if (Preview > 0)
            {
                Profile = ShowDDL = Drop = Truncate = false;
                NoLoad = true;
            }

            if (Drop && !Profile)
            {
                // if we're dropping the table, we have to profile the data to create the table
                Profile = true;
            }
        }

        /// <summary>
        /// Returns null if the passed string is null or empty, otherwise returns the passed string
        /// </summary>
        /// <param name="s">The string to evaluate</param>
        /// <returns>null if the passed string is null or empty, otherwise returns the passed string</returns>
        private static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>
        /// Returns the passed table name as a valid SQL identifier. If already valid, then returns unchanged. Always returns
        /// the table name with a schema prefixed. (E.g. "dbo.frobozz")
        /// </summary>
        /// <param name="TableName"></param>
        /// <returns>The table name prefixed with the schema. E.g. "dbo.foo"</returns>
        private static string MakeTableNameValid(string TableName)
        {
            string Schema = ServerUtils.SchemaFromTableName(TableName);
            string Table = ServerUtils.ObjectFromObjName(TableName);
            string NewTableName = string.Empty;
            if (!Table[0].Equals('_') && !char.IsLetter(Table[0]))
            {
                NewTableName += "_";
            }
            foreach (char c in Table)
            {
                NewTableName += (c.Equals("_") || char.IsLetterOrDigit(c)) ? c : '_';
            }
            return Schema + "." + NewTableName;
        }
        private Cfg() { }
    }
}
