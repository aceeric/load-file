using AppSettings;
using System;
using System.Collections.Generic;
using System.IO;

namespace load_file
{
    /// <summary>
    /// Extends the AppSettingsBase class with settings needed by the utility
    /// </summary>

    class AppSettingsImpl : AppSettingsBase
    {
        /// <summary>
        /// Specifies the pathname of the file to load
        /// </summary>
        public static StringSetting File { get { return (StringSetting)SettingsDict["File"]; } }

        /// <summary>
        /// Specifies the pathname of the file to log error records to
        /// </summary>
        public static StringSetting ErrFile { get { return (StringSetting)SettingsDict["ErrFile"]; } }

        /// <summary>
        /// Defines a SQL timeout in hours
        /// </summary>
        public static StringSetting SQLTimeout { get { return (StringSetting)SettingsDict["SQLTimeout"]; } }

        /// <summary>
        /// Specifies the pathname of the file to generate frequencies into
        /// </summary>
        public static StringSetting FreqFile { get { return (StringSetting)SettingsDict["FreqFile"]; } }

        /// <summary>
        /// Specifies the target database to load the data into
        /// </summary>
        public static StringSetting Db { get { return (StringSetting)SettingsDict["Db"]; } }

        /// <summary>
        /// Specifies the server name (if not specified, localhost is used)
        /// </summary>
        public static StringSetting Server { get { return (StringSetting)SettingsDict["Server"]; } }

        /// <summary>
        /// Specifies the target table to load the data into. Can be a bare table name, or in the
        /// form schema.tablename.
        /// </summary>
        public static StringSetting Tbl { get { return (StringSetting)SettingsDict["Tbl"]; } }

        /// <summary>
        /// True if the user wants to profile the source data and ensure data type compatibility with the
        /// target table (if the target table already exists)
        /// </summary>
        public static BoolSetting Profile { get { return (BoolSetting)SettingsDict["Profile"]; } }

        /// <summary>
        /// Specifies that the user wants to preview the first n lines of the file, but perform
        /// no further processing
        /// </summary>
        public static IntSetting Preview { get { return (IntSetting)SettingsDict["Preview"]; } }

        /// <summary>
        /// Indicates that the target table should not be loaded
        /// </summary>
        public static BoolSetting NoLoad { get { return (BoolSetting)SettingsDict["NoLoad"]; } }

        /// <summary>
        /// True to display the table DDL to the console
        /// </summary>
        public static BoolSetting ShowDDL { get { return (BoolSetting)SettingsDict["ShowDDL"]; } }

        /// <summary>
        /// True if file should be loaded via BCP instead of SqlBulkCopy
        /// </summary>
        public static BoolSetting BCP { get { return (BoolSetting)SettingsDict["BCP"]; } }

        /// <summary>
        /// True if file should be prepped prior to load
        /// </summary>
        public static BoolSetting Prep { get { return (BoolSetting)SettingsDict["Prep"]; } }

        /// <summary>
        /// A directory to hold prepped files. If not specified, then prepped files are placed in the same folder as the input file
        /// </summary>
        public static StringSetting PrepDir { get { return (StringSetting)SettingsDict["PrepDir"]; } }

        /// <summary>
        /// True if prepped file should be retained after successful load. Otherwise deleted
        /// </summary>
        public static BoolSetting PrepRetain { get { return (BoolSetting)SettingsDict["PrepRetain"]; } }

        /// <summary>
        /// Defines the pathname of a split columns file
        /// </summary>
        public static StringSetting Split { get { return (StringSetting)SettingsDict["Split"]; } }

        /// <summary>
        /// Defines a different split character. (The default is the colon character)
        /// </summary>
        public static StringSetting SplitStr { get { return (StringSetting)SettingsDict["SplitStr"]; } }

        /// <summary>
        /// Indicates that if the target table does not exist - and is created by the utility - that
        /// the table is created with typed column names. Otherwise created with VARCHAR column names
        /// </summary>
        public static BoolSetting Typed { get { return (BoolSetting)SettingsDict["Typed"]; } }

        /// <summary>
        /// Indicates that if the target table - if it exists - should be truncated before loading
        /// </summary>
        public static BoolSetting Truncate { get { return (BoolSetting)SettingsDict["Truncate"]; } }

        /// <summary>
        /// Indicates that if the target table - if it exists - should be dropped before loading
        /// </summary>
        public static BoolSetting Drop { get { return (BoolSetting)SettingsDict["Drop"]; } }

        /// <summary>
        /// Specifies the field delimiter to use
        /// </summary>
        public static StringSetting Delimiter { get { return (StringSetting)SettingsDict["Delimiter"]; } }

        /// <summary>
        /// Supports parsing a fixed field-width file. Returns the list of field widths
        /// </summary>
        public static StringListSetting Fixed { get { return (StringListSetting)SettingsDict["Fixed"]; } }

        /// <summary>
        /// For fixed field parsing, specifies the number of characters to expand tabs to
        /// </summary>
        public static IntSetting TabSize { get { return (IntSetting)SettingsDict["TabSize"]; } }

        /// <summary>
        /// Specifies the max data rows in the source file to process
        /// </summary>
        public static IntSetting MaxRows { get { return (IntSetting)SettingsDict["MaxRows"]; } }

        /// <summary>
        /// If True, simply splits incoming fields on the defined delimiter (does not handle quote-enclosed fields)
        /// </summary>
        public static BoolSetting SimpleParse { get { return (BoolSetting)SettingsDict["SimpleParse"]; } }

        /// <summary>
        /// Specifies the initial number of lines in the file to skip
        /// </summary>
        public static IntSetting SkipLines { get { return (IntSetting)SettingsDict["SkipLines"]; } }

        /// <summary>
        /// Specifies the maximum number of errors to accommodate before aborting
        /// </summary>
        public static IntSetting MaxErrors { get { return (IntSetting)SettingsDict["MaxErrors"]; } }

        /// <summary>
        /// Specifies the one-relative row in the file that contains a header.
        /// </summary>
        public static IntSetting HeaderLine { get { return (IntSetting)SettingsDict["HeaderLine"]; } }

        /// <summary>
        /// Defines and EOF string which - if encountered - terminates file processing
        /// </summary>
        public static StringSetting EOFStr { get { return (StringSetting)SettingsDict["EOFStr"]; } }

        /// <summary>
        /// Path of a file containing a list of column names for the source file (useful if the sourcefile
        /// does not have a header)
        /// </summary>
        public static StringSetting ColFile { get { return (StringSetting)SettingsDict["ColFile"]; } }

        /// <summary>
        /// Logging target (e.g. file, database, console)
        /// </summary>
        public static StringSetting Log { get { return (StringSetting)SettingsDict["Log"]; } }

        /// <summary>
        /// Specifies which type of events are logged
        /// </summary>
        public static StringSetting LogLevel { get { return (StringSetting)SettingsDict["LogLevel"]; } }

        /// <summary>
        /// Specifies the Job ID
        /// </summary>
        public static StringSetting JobID { get { return (StringSetting)SettingsDict["JobID"]; } }

        public new static string RegKey
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Initializes the instance with an array of settings that the utility supports, as well as usage instructions
        /// </summary>

        static AppSettingsImpl()
        {
            SettingList = new Setting[] {
                new StringSetting("File", "filespec", null,  Setting.ArgTyp.Mandatory, true, false,
                    "The path name of the file to load. Only a single file at a time is supported."),
                new StringSetting("Server", "server", "localhost",  Setting.ArgTyp.Optional, true, false,
                    "The server to load the data into. If not specified, localhost is used. Only Windows Integrated " +
                    "login is supported. Required unless -noload is specified."),
                new StringSetting("Db", "database", null,  Setting.ArgTyp.Optional, true, false,
                    "The database to load the data into. Required unless -noload is specified."),
                new StringSetting("Tbl", "schema.table", null,  Setting.ArgTyp.Optional, true, false,
                    "The table to load. If a dotted schema prefix is provided, then it is used, otherwise the table is accessed in the " +
                    "dbo schema. If the table does not exist, it is created. If a table name is not specified, then the utility " +
                    "creates a table in the dbo schema of the supplied database - generating a unique table name from the source " +
                    "file name. If a target table name is not specified, or, a table name is specified for a non-existent table, " +
                    "then the utility will profile the data regardless of whether the -profile arg is supplied - so that it can " +
                    "determine the applicable data types for the columns in the table."),
                new BoolSetting("Profile", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that the source data will be profiled. If the target table exists, the utility will ensure that the table " +
                    "is capable of storing the incoming data. If the table is not capable, then the utility will emanate an error message " +
                    "and stop. If profiling is not specified, then the load may fail due to data type incompatibilities between the source " +
                    "data and the target table. However, profiling can take a substantial amount of time for a large file so - if there is " +
                    "high confidence in data compatibility then profiling can be dispensed with when loading an existing table. (If the " +
                    "utility creates the table, then profiling will be performed no matter what.)"),
                new BoolSetting("BCP", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that Microsoft BULK INSERT will be used to load the data. BULK INSERT cannot skip headers so - if the " +
                    "incoming data has a header and the BCP option is specified, then the file must be prepped first using the -prep " + 
                    "option or the load will fail. If this arg is not provided, then SqlBulkCopy is used to load the data, which is " +
                    "significantly slower than BULK INSERT."),
                new BoolSetting("Prep", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that the source data will be prepped for bulk loading. If the source file contains headers, footers, or " +
                    "is quoted, then it cannot be loaded via BULK INSERT. To use BULK INSERT, the file must be \"prepped\", which consists of removing " +
                    "headers and footers, and un-quoting and tab-delimiting the fields in the file. The prepped file will be placed in " +
                    "the same directory as the source file, with a suffix attached to the filename base. E.g. \"some-file.csv\" will get " +
                    "prepped into \"some-file.prepped.csv\". The utility will ensure there are no file name collisions by postfixing a " +
                    "number where required (e.g. \"some-file.prepped-1.csv\"). Note - the prepped file is always written out as a " +
                    "tab-delimited file. Any tabs in incoming data fields will be replaced by spaces so that the presence of " +
                    "tabs in the prepped file can definitively be interpreted as field separators."),
                new StringSetting("PrepDir", "directory", null, Setting.ArgTyp.Optional, true, false,
                    "Ignored if the -prep arg is not supplied. Specifies a directory to hold prepped files. The supplied value must be the " +
                    "name of an existing directory that the user has rights to create files in. If not provided, then prepped files are " +
                    "created in the same directory as the source file."),
                new BoolSetting("PrepRetain", false, Setting.ArgTyp.Optional, true, false,
                    "Ignored if the -prep arg is not supplied. Indicates that prepped files should not be deleted when the utility " +
                    "finishes. (Original files are never modified or removed.) The default is to delete prepped files."),
                new BoolSetting("Typed", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that if the target table is to be created by the utility, it will be created with data types that match the " +
                    "source file data. This means, for example, that if the profiler determines that a column in the " +
                    "source file is an integer, it will define the column in the database table as an integer. If this arg is not specified, " +
                    "then all table columns are created as VARCHAR with a size sufficient to hold the source data. Has no effect unless " +
                    "the utility creates the target table. Note - omitting this argument significantly increases the performance of the " +
                    "data profiling step but could slow down the bulk import."),
                new StringSetting("Split", "filespec", null, Setting.ArgTyp.Optional, true, false,
                    "Path name of a file that contains the names of columns to be split into two columns: code " +
                    "and description. The named file must contain exactly one column name on each line. The default " +
                    "behavior is not to perform any splitting. If a split file is provided, the default split string " +
                    "is the colon character (:). If a column is split on a different string, then the split string for " +
                    "that column can be specified in the split file by postfixing a column name with a comma, followed " +
                    "by a bare or quote-enclosed split string. E.g.: naics,\"~\" would split the naics field into naics " +
                    "and naics_descr on the tilde character. Note - the file must have a header or a column name file must " +
                    "be supplied for splitting to work. The second column created by the utility is named the same as the " +
                    "original column with \"_descr\" appended. If splitting is specified, then the -prep arg is required."),
                new StringSetting("SplitStr", "str", null, Setting.ArgTyp.Optional, true, false,
                    "The string to split on. If not supplied, colon (':') is used. Column-specific overrides " +
                    "override this, and the default split string. Ignored unless -split is specified."),
                new BoolSetting("ShowDDL", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that the utility should display to the console the DDL statement that it would use to create the " +
                    "target table. If omitted, no DDL is displayed"),
                new BoolSetting("Truncate", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that the target table - if it exists - is to be truncated before being loaded. If not specified, then " +
                    "data is appended to an existing table. Ignored if the table does not exist, or will be dropped first."),
                new BoolSetting("NoLoad", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that the target table is not to be loaded. In this scenario, the utility performs all the other specified " +
                    "functions - profile, prep, frequency-generation - but does not actually load the server table. It does not perform " +
                    "any server-related activity - operating solely on the local file system objects. All server-related args are ignored."),
                new BoolSetting("Drop", false, Setting.ArgTyp.Optional, true, false,
                    "Indicates that the target table - if it exists - is to be dropped before being loaded. In this case " +
                    "the utility will profile the data because it needs to determine the data types for the columns. " +
                    "If not specified, and an existing table name is supplied, then the table is loaded. Ignored if the " +
                    "table does not exist."),
                new FieldWidthsListSetting("Fixed", "list|@list", null, Setting.ArgTyp.Optional, true, false,
                    "Indicates that the source file is fixed field-width file. The argument value is a comma-separated list of field " +
                    "widths, or the name of a file containing field widths. If this arg is specified, then the -delimiter arg and the " +
                    "-simpleparse args are ignored. If the list specifier is in the form nnn,nnn,nnn... then it is interpreted as a " +
                    "comma-separated list of field widths. If the list specifier begins with the at sign (@) then the at sign is removed " +
                    "and the remainder of the list specifier is interpreted as a filename. The file is opened and the field widths " +
                    "are built from the file. The file can contain multiple lines, with multiple width specifiers per line, separated "+
                    "by commas. If this arg is omitted, then the input file is treated as a delimited file."),
                new IntSetting("TabSize", "n", 4, Setting.ArgTyp.Optional, true, false,
                    "Only used if the -fixed argument is provided. Causes tabs in the input file to be expanded to the specified number " +
                    "of spaces. If not provided, the default is for tabs to be expanded to four spaces."),
                new StringSetting("Delimiter", "delim", "tab",  Setting.ArgTyp.Optional, true, false,
                    "Specifies the field delimiter for the input file. Allowed literals are 'pipe', 'comma', " +
                    "'tab', and 'auto'. E.g. '-delimiter tab'. If not supplied, then tab is used as the default. If 'auto' is specified " +
                    "then the utility will attempt to determine the delimiter by scanning the first thousand records of the file and " +
                    "looking for a delimiter from the supported set that consistently splits the lines in the file."),
                new IntSetting("MaxRows", "n", int.MaxValue, Setting.ArgTyp.Optional, true, false,
                    "Process at most n data rows from the file. (Does not include header or skipped rows.) The default is to " +
                    "process all input rows."),
                new IntSetting("Preview", "n", 0, Setting.ArgTyp.Optional, true, false,
                    "Displays the first n rows of the file to the console and then exits without performing any additional " +
                    "processing. Useful to validate that the input file is being properly parsed by the utility."),
                new BoolSetting("SimpleParse", false, Setting.ArgTyp.Optional, true, false,
                    "If supplied, the utility will not attempt to perform field parsing based on quotes in the " +
                    "incoming file. It will simply split each line on the specified delimiter. Useful in cases where " +
                    "the file to load does not contain delimiters embedded within fields. If not provided then the utility " +
                    "performs quote parsing of the file data to handle embedded delimiters (which slows the process.)"),
                new IntSetting("SkipLines", "n", null, Setting.ArgTyp.Optional, true, false,
                    "Do not process the first n lines of the input file. The utility will read those lines and discard them. " +
                    "Applicable if the file has a header row or rows. The default is to process all input lines."),
                new IntSetting("HeaderLine", "n", null, Setting.ArgTyp.Optional, true, false,
                    "Defines the 1-relative line number in the file that contains a header. If provided, and the utility " +
                    "is directed to create the target table, then the utility will use the column names from the header row, " +
                    "after converting them to valid SQL identifiers. If this arg is specified, then skiplines can be omitted " +
                    "if the header row is the only non-data row at the head of the file. " +
                    "If both are specified, then both values are used independently, not additively or relatively. For example, " +
                    "if -skiplines=1 and -headerline=1, then the utility will read the header from line one (1-rel), and also ignore " +
                    "that line (it is skipped.) If -skiplines=5 and -headerline=1, then the utility will read the header from line one, " +
                    "ignore that line, and ignore the next four lines for a total of 5 skipped lines. The -headerline value " +
                    "cannot be greater than -skiplines. If the file has a header, but there is no desire to use it to generate " +
                    "column names, then skip over it with -skiplines, and omit this arg."),
                new StringSetting("EOFStr", "eofstr", null,  Setting.ArgTyp.Optional, true, false,
                    "Some files have an EOF line as the last line. This line begins with a specific string. If the file " +
                    "being processed has an EOF line, then provide the first part of the EOF string here and the utility " +
                    "will stop processing when the string is encountered at the start of the first matching line. " +
                    "Note: the match is an exact match, including case. If the EOF string contains a space, enclose " +
                    "the string in quotes. (E.g. -eofstr \"EOF PUBLIC\")"),
                new StringSetting("ColFile", "filespec", null,  Setting.ArgTyp.Optional, true, false,
                    "Applicable if the utility is going to create the target table. If supplied, the utility " +
                    "will read the column names from the specified file. The order of columns in the column names " +
                    "file should match that of the source file. The column name file must have one column name per row. " +
                    "If the column names file is supplied, the table will be created with those names. (This arg overrides the -headerline arg.) " +
                    "If omitted, and the source file does not have a header, and the utility is directed to create the target table, " +
                    "then the target table column names will be named col1, col2... etc. NOTE: If a column file is specified, then " +
                    "it must not include additional column names for splitting. It must align with the original unsplit file. (Splitting " +
                    "will generate the new column names.)"),
                new IntSetting("MaxErrors", "n", null,  Setting.ArgTyp.Optional, true, false,
                    "Specifies the maximum number of error records that the utility will allow before aborting. An error record is defined " +
                    "as one having an incorrect column count. The default value is zero: i.e. the first error causes the process to " +
                    "abort. A value of -1 indicates that all errors are to be ignored. The column count for the file is determined by " +
                    "the header line, the column names file, or the first non-skipped line."),
                new StringSetting("ErrFile", "filespec", null,  Setting.ArgTyp.Optional, true, false,
                    "Indicates that the utility should write records to the specified error file if the number of columns in any given row " +
                    "don't match the rest of the file. The \"correct\" number of columns is determined by the header, if one " +
                    "exists, or the column names file, if specified, otherwise the first non-skipped record in the file."),
                new StringSetting("FreqFile", "filespec", null,  Setting.ArgTyp.Optional, true, false,
                    "Ignored unless the -profile arg is provided. Specifies the path of a file to write frequencies to. Each column " +
                    "in the input file will be freq'd in the freq file unless the number of distinct values for a column exceeds 1000, " +
                    "then only the first 1000 values will be freq'd for that column. Note: generating frequencies will increase the " +
                    "profiling time for large files."),
                new StringSetting("SQLTimeout", "hrs", "1",  Setting.ArgTyp.Optional, true, false,
                    "Specifies the command timeout for executing SQL commands against the server. The value specified is in hours. Decimal values are " +
                    "allowed (e.g. .25 for 15 minutes). If this arg is not provided, then one (1) hour is used as the default."),
                new StringSetting("Log", "file|db|con", "con",  Setting.ArgTyp.Optional, true, false,
                    "Determines how the utility communicates errors, status, etc. If not supplied, then all output goes to the console. " +
                    "If 'file' is specified, then the utility logs to a log file in the same directory that the utility is run from. " +
                    "The log file will be named load-file.log. " +
                    "If 'db' is specified, then logging occurs to the database. If 'con' is specified, then output goes to the console " +
                    "(same as if the arg were omitted.) If logging to file or db is specified then the utility runs silently " +
                    "with no console output. If db logging is specified, then the required logging components must be " +
                    "installed in the database. If the components are not installed and db logging is specified, then the utility " +
                    "will automatically fail over to file-based logging."),
                new StringSetting("LogLevel", "err|warn|info", "info",  Setting.ArgTyp.Optional, true, false,
                    "Defines the logging level. 'err' specifies that only errors will be reported. 'warn' means errors and warnings, " +
                    "and 'info' means all messages. The default is 'info'."),
                new StringSetting("JobID", "guid", null,  Setting.ArgTyp.Optional, true, false,
                    "Defines a job ID for the logging subsystem. A GUID value is supplied in the canonical 8-4-4-4-12 form. If provided, " +
                    "then the logging subsystem is initialized with the provided GUID. The default behavior is for the logging subsystem " +
                    "to generate its own GUID.")
            };

            Usage =
                "Loads a delimited flat file into a database table. Optionally profiles the data first. If a target table is specified, " +
                "and the table exists, verifies that the table will accommodate the data types in the file and halts with an error " +
                "if the data in the file is not compatible with the table. If a target table is specified and does not exist, " +
                "the utility creates the table. If no target table is specified, then generates a unique table name from the " +
                "loaded file name. Columns in the source file are loaded into the target table by ordinal position (not column name.)";
        }

        /// <summary>
        /// Performs custom arg validation for the utility, after invoking the base class parser.
        /// </summary>
        /// <param name="Settings">A settings instance to parse</param>
        /// <param name="CmdLineArgs">Command-line args array</param>
        /// <returns>True if args are valid, else False</returns>

        public new static bool Parse(SettingsSource Settings, string[] CmdLineArgs = null)
        {
            if (AppSettingsBase.Parse(Settings, CmdLineArgs))
            {
                if (HeaderLine.Initialized && SkipLines.Initialized &&
                    HeaderLine.Value > SkipLines.Value)
                {
                    ParseErrorMessage = "The header line value cannot be greater than the skip lines value";
                    return false;
                }
                if (HeaderLine.Initialized && ColFile.Initialized)
                {
                    ParseErrorMessage = "Specify either a header line value or a column file value, but not both";
                    return false;
                }
                if (Tbl.Initialized && Tbl.Value.Contains("."))
                {
                    if (Tbl.Value.Split('.').Length != 2)
                    {
                        ParseErrorMessage = "Accepted table name forms are \"tablename\" and \"schemaname.tablename\"";
                        return false;
                    }
                }
                if (Delimiter.Initialized)
                {
                    if (!Delimiter.Value.In("pipe", "comma", "tab", "auto"))
                    {
                        ParseErrorMessage = "Invalid value specified for the -delimiter arg";
                        return false;
                    }
                }
                if (JobID.Initialized && !JobID.Value.IsGuid())
                {
                    ParseErrorMessage = "-jobid arg must be a GUID (nnnnnnnn-nnnn-nnnn-nnnn-nnnnnnnnnnnn)";
                    return false;
                }
                if (!Db.Initialized && !NoLoad.Value && Preview.Value <= 0)
                {
                    ParseErrorMessage = "-db argument is required unless the -noload argument is specified";
                    return false;
                }
                if (Split.Initialized && !Prep.Initialized && Preview.Value <= 0)
                {
                    ParseErrorMessage = "Splitting requires the -prep argument, because the input file has to be split out to a prep file.";
                    return false;
                }
                if (SQLTimeout.Initialized)
                {
                    float Timeout;
                    if (!float.TryParse(SQLTimeout.Value, out Timeout))
                    {
                        ParseErrorMessage = "Uable to parse the specified SQL timeout value as a decimal value: " + SQLTimeout.Value;
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Supports the ability to provide fixed field widths on the command line or from a file
        /// </summary>
        private class FieldWidthsListSetting : StringListSetting
        {
            /// <summary>
            /// Initializes the instance. Simply passes control to the parent class constructor
            /// </summary>
            /// <param name="Key"></param>
            /// <param name="ArgValHint"></param>
            /// <param name="DefaultValue"></param>
            /// <param name="Help"></param>

            public FieldWidthsListSetting(string Key, string ArgValHint, List<string> DefaultValue, ArgTyp ArgType, bool Persist, bool IsInternal, string Help)
                : base(Key, ArgValHint, DefaultValue, ArgType, Persist, IsInternal, Help) { }

            /// <summary>
            /// Accepts a value that is either a comma-separated list of DUNS numbers or in the form @filename in which
            /// filename is a file containing DUNS numbers
            /// </summary>
            /// <param name="Key"></param>
            /// <param name="Value"></param>
            /// <returns></returns>

            public override bool Accept(string Key, string Value)
            {
                if (Key.ToLower() == SettingKey.ToLower())
                {
                    if (Value != string.Empty && Value.Substring(0, 1) == "@")
                    {
                        using (StreamReader sr = new StreamReader(Value.Substring(1)))
                        {
                            while ((Value = sr.ReadLine()) != null)
                            {
                                SettingValue.AddRange(Value.Split(','));
                                SettingInitialized = true;
                            }
                        }
                    }
                    else
                    {
                        SettingValue.AddRange(Value.Split(','));
                        SettingInitialized = true;
                    }
                    SettingValue.RemoveAll(Str => string.IsNullOrEmpty(Str)); // ensure there are no empties
                    return true;
                }
                return false;
            }
        }
    }
}
