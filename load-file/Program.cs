using AppSettings;
using System;
using System.Collections.Generic;
using System.IO;
using static load_file.Globals;

/*
 * TODO
 * ---------------------------------------------------------------------------
 * Handle drop/recreate/truncate VIEW as well as TABLE
 * Consider an upsize feature - expand data types if needed
 * Have an option to clean up column names
 * Support -fixed auto - auto-calcs the field widths
 * Handle custom logic like parsing a filename and getting info from it into the table (e.g. month and year)
 * Complete unit tests
 * Consider an idcol arg - if specified, the prepper inserts a column into the file
 *    at the front that it leaves empty. Assumption is the table has been prepped with
 *    a default for that column. Or - some better approach?
 * Consider an "auto-adapt" feature - for small tables - handles column order change - adds new columns to an existing table
 *    if they appear in a data file.
 * Handle Excel files using the open SDK
 */

namespace load_file
{
    /// <summary>
    /// Defines the exit codes supported by the utility
    /// </summary>

    enum ExitCode : int
    {
        /// <summary>
        /// Indicates successful completion
        /// </summary>
        Success = 0,
        /// <summary>
        /// Indicates that invalid settings or command line args were provided
        /// </summary>
        InvalidParameters = 1,
        /// <summary>
        /// Indicates the source file specified by the user does not exist
        /// </summary>
        SrcFileDoesNotExist = 2,
        /// <summary>
        /// Indicates an inability to connect to the database server
        /// </summary>
        DBConnectFailed = 3,
        /// <summary>
        /// Indicates the supplied database name does not exist
        /// </summary>
        InvalidDatabaseName = 4,
        /// <summary>
        /// Indicates the schema name embedded in the table name does not exist
        /// </summary>
        InvalidSchemaName = 5,
        /// <summary>
        /// Indicates the bulk load failed
        /// </summary>
        LoadTableFailed = 6,
        /// <summary>
        /// Indicates the source file specified by the user does not exist
        /// </summary>
        ColFileDoesNotExist = 7,
        /// <summary>
        /// Indicates the source file is not a text file
        /// </summary>
        SrcFileIsNotText = 8,
        /// <summary>
        /// Indicates the colname file is not a text file
        /// </summary>
        ColFileIsNotText = 9,
        /// <summary>
        /// Indicates the column splitting file specified by the user does not exist
        /// </summary>
        SplitFileDoesNotExist = 10,
        /// <summary>
        /// Indicates the target table data types will not accommodate the incoming file data types
        /// </summary>
        TargetTableDataTypesNotCompatible = 11,
        /// <summary>
        /// Indicates the target table data types will not accommodate the incoming file data types
        /// </summary>
        CouldNotDetermineDelimiter = 12,
        /// <summary>
        /// Indicates that some other error occurred
        /// </summary>
        OtherError = 99
    }

    /// <summary>
    /// Entry point
    /// </summary>
    class Program
    {
        /// <summary>
        /// Entry point. Does initialization and then calls the DoWork method to actually do the work
        /// </summary>
        /// <param name="args">provided by .Net</param>

        static void Main(string[] args)
        {
#if DEBUG
            args = new string[] {
                "-file", @"D:\Personal\Beau Senyard\DATA\USASpending\datafeeds\2018_All_Contracts_Delta_20180215.tsv"
                , "-profile"
                , "-showddl"
                , "-db", "ingest"
                , "-tbl", "usasp_delta"
                , "-bcp"
                , "-prep"
                , "-drop"
                , "-delimiter", "auto"
                , "-split", "./splitcols.txt"
                , "-splitstr", ":"
                , "-maxerrors", "-1"
                , "-headerline", "1"
                , "-log", "con"
                , "-loglevel", "info"

//              , "-sqltimeout", "2"
//              , "-prepdir", "./"
//              , "-prepretain"
//              , "-typed"
//              , "-truncate"
//              , "-noload"
//              , "-fixed", "255,20,16,28,22,20,255,20,255,20,255,25,255,20,255,25,255,25,255,50,23,23,23,23,20,20,255,21,255,21,255,32,20,255,20,20,255,255,44,255,20,255,50,29,4000,27,15,26,255,16,17,17,20,20,20,24,50,50,255,255,255,255,50,26,20,25,12,255,255,50,255,50,20,20,255,50,20,21,50,50,20,20,50,50,23,23,255,20,20,20,50,20,50,29,255,25,20,39,20,20,255,20,255,20,255,18,255,43,50,255,26,50,24,255,20,20,255,255,20,21,20,255,29,255,20,255,255,50,17,11,20,50,50,50,20,255,20,50,22,35,41,25,48,20,21,27,22,255,20,255,20,20,255,20,20,35,50,20,50,20,20,20,40,21,20,26,20,20,20,20,23,20,20,21,25,20,20,20,20,20,25,31,23,20,20,30,25,20,43,50,22,22,22,20,20,28,20,44,20,20,20,26,20,22,34,20,20,20,20,24,29,24,28,45,21,40,33,20,21,29,23,20,25,43,20,20,31,27,22,22,29,39,29,20,42,31,30,20,25,20,20,37,26,25,35,37,47,50,50,50,20,23,255,37,255,37,255,37,255,37,255,37,31,50,23"
//              , "-maxrows", "10"
//              , "-simpleparse"
//              , "-errfile", ".\error.txt"
//              , "-freqfile", "./frequencies.txt"
//              , "-skiplines", "1"
//              , "-eofstr", "EOF PUBLIC"
//              , "-colfile", ".\\samcols.txt"
//              , "-jobid", "12345678-1234-2345-3456-1234567890ab"
            };
#endif

            try
            {
                if (!ParseArgs(args))
                {
                    Environment.ExitCode = (int)ExitCode.InvalidParameters;
                    return;
                }
                Cfg.Init();
                Log.InitLoggingSettings();
                Log.InformationMessage("Started");
                if (DoValidations())
                {
                    DoWork(); // sets the exit code
                }
                Log.InformationMessage("Normal completion");
            }
            catch (Exception Ex)
            {
                if (Ex is LoadException)
                {
                    Log.ErrorMessage(Ex.Message);
                }
                else
                {
                    Log.ErrorMessage("An unhandled exception occurred. The exception was: {0}. Stack trace follows:\n{1}", Ex.Message, Ex.StackTrace);
                }
                Environment.ExitCode = (int)ExitCode.OtherError;
            }
        }

        /// <summary>
        /// Executes a number of start-up validations and initializations. Emanates the appropriate error message
        /// and sets the ExitCode if unable to proceed
        /// </summary>
        /// <returns>true if the utility can proceed, else false: the utility is unable to proceed</returns>

        static bool DoValidations()
        {
            if (Cfg.NoLoad && !Cfg.Profile && !Cfg.ShowDDL && Cfg.Preview <= 0)
            {
                Log.ErrorMessage("The -noload arg was specified, the -profile arg was not specified, -preview was not specified, and -showddl was not specified. Nothing to do.");
                Environment.ExitCode = (int)ExitCode.InvalidParameters;
                return false;
            }

            if (!Cfg.Profile && Cfg.ShowDDL)
            {
                Log.InformationMessage("The -profile arg was not specified, but -showddl was specified. Profiling is being enabled anyway for DDL generation.");
                Cfg.Profile = true;
            }

            if (Cfg.Preview > 0)
            {
                Log.InformationMessage("The -preview was specified. All other processing options will be ignored.");
            }

            if (Cfg.Drop && !Cfg.Profile)
            {
                Log.InformationMessage("The -drop arg was specified, but -profile was not specified. Enabling profiling anyway for DDL generation.");
                Cfg.Profile = true; // if we're dropping the table, we have to profile the data to create the table
            }

            if (!File.Exists(Cfg.File))
            {
                Log.ErrorMessage("Specified file to load does not exist: {0}", Cfg.File);
                Environment.ExitCode = (int)ExitCode.SrcFileDoesNotExist;
                return false;
            }
            else if (!FileProcessor.IsTextFile(Cfg.File))
            {
                Log.ErrorMessage("Specified file to load does not appear to be a text file: {0}", Cfg.File);
                Environment.ExitCode = (int)ExitCode.SrcFileIsNotText;
                return false;
            }

            if (AppSettingsImpl.Delimiter.Value.ToLower() == "auto")
            {
                char c = DelimitedLineParser.CalcDelimiter(Cfg.File);
                if (c == (char)0)
                {
                    Log.ErrorMessage("'auto' was specified as the delimiter, but the utility was unable to determine the delimiter from the data file.");
                    Environment.ExitCode = (int)ExitCode.CouldNotDetermineDelimiter;
                    return false;
                }
                Cfg.Delimiter = c;
                Log.InformationMessage("Obtained delimiter from file: {0}", c.Xlat(new char[] { '\t', '|', ',' }, new string[] { "tab", "pipe", "comma" }));
            }

            if (Cfg.ColFile != null && !File.Exists(Cfg.ColFile))
            {
                Log.ErrorMessage("Specified column header file not exist: {0}", Cfg.ColFile);
                Environment.ExitCode = (int)ExitCode.ColFileDoesNotExist;
                return false;
            }
            if (Cfg.ColFile != null && !FileProcessor.IsTextFile(Cfg.ColFile))
            {
                Log.ErrorMessage("Specified column names file does not appear to be a text file: {0}", Cfg.ColFile);
                Environment.ExitCode = (int)ExitCode.ColFileIsNotText;
                return false;
            }
            if (Cfg.Split != null && !File.Exists(Cfg.Split))
            {
                Log.ErrorMessage("File specified for column splitting not exist: {0}", Cfg.Split);
                Environment.ExitCode = (int)ExitCode.SplitFileDoesNotExist;
                return false;
            }
            if (ShouldLoadTable())
            {
                // only perform server-related validations if the user wants to load the server table
                if (!ServerUtils.CanConnect(Cfg.Server))
                {
                    Log.ErrorMessage("Unable to connect to the specified SQL Server: {0}", Cfg.Server);
                    Environment.ExitCode = (int)ExitCode.DBConnectFailed;
                    return false;
                }

                if (!ServerUtils.IsValidDatabaseName(Cfg.Server, Cfg.Db))
                {
                    Log.ErrorMessage("Specified database does not exist on the server: {0}", Cfg.Db);
                    Environment.ExitCode = (int)ExitCode.InvalidDatabaseName;
                    return false;
                }

                if (!ServerUtils.IsValidSchemaName(Cfg.Server, Cfg.Db, Cfg.Schema))
                {
                    Log.ErrorMessage("Specified schema {0} is invalid in database {1}", Cfg.Schema, Cfg.Db);
                    Environment.ExitCode = (int)ExitCode.InvalidSchemaName;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Main worker method
        /// </summary>

        static void DoWork()
        {
            List<ProfileColumn> Cols = null;
            Dictionary<string, string> SplitCols = null;
            string PrepFileFqpn = null;

            if (ShouldSplitCols())
            {
                SplitCols = GetSplitCols();
            }
            if (ShouldPreview())
            {
                Preview(SplitCols);
                Environment.ExitCode = (int)ExitCode.Success;
                return;
            }
            if (ShouldPreprocess())
            {
                Cols = Preprocess(SplitCols, out PrepFileFqpn);
            }
            if (ShouldShowDDL())
            {
                ShowDDL(Cols);
            }
            if (ShouldGenerateFreqs())
            {
                GenerateFreqs(Cols);
            }
            if (!ShouldLoadTable())
            {
                Log.InformationMessage("The -noload arg was specified. Stopping.");
                Environment.ExitCode = (int)ExitCode.Success;
            }
            else
            {
                if (ShouldDropTable())
                {
                    DropTable();
                }
                if (ShouldCreateTable())
                {
                    CreateTable(Cols);
                }
                if (!DataWillLoad(Cols, Cfg.Typed))
                {
                    Log.InformationMessage("Process is stopping");
                    Environment.ExitCode = (int)ExitCode.TargetTableDataTypesNotCompatible;
                    return;
                }
                if (ShouldTruncateTableOrView())
                {
                    TruncateTableOrView();
                }
                if (LoadTable(PrepFileFqpn))
                {
                    ServerUtils.GenerateCompletionMessage(Cfg.Server, Cfg.Db, Cfg.Tbl);
                    Environment.ExitCode = (int)ExitCode.Success;
                }
                else
                {
                    Environment.ExitCode = (int)ExitCode.LoadTableFailed;
                }
            }
            if (ShouldRemovePrepFile())
            {
                RemovePrepFile(PrepFileFqpn);
            }
            else if (Cfg.Prep)
            {
                Log.InformationMessage("Prep file available at: {0}", PrepFileFqpn);
            }
        }

        /// <summary>
        /// Displays a user-specified number of lines to the console
        /// </summary>
        /// <param name="SplitCols">A Dictionary in which each key is the name of a column and each value is the 
        /// character to split that column on</param>
        static void Preview(Dictionary<string, string> SplitCols)
        {
            FileProcessor.Preview(SplitCols);
        }

        /// <summary>
        /// Validates that the target table data types are compatible with the incoming data in the file to load.
        /// </summary>
        /// <remarks>
        /// If profiling is not enabled, then we have no idea if the data is compatible so simply report "true" and the
        /// load will fail if there is a data type incompatibility
        /// </remarks>
        /// <returns>
        /// true if profiling is not enabled. If profiling is enabled, then returns true if the database table will
        /// hold the incoming data.
        /// <param name="Cols">data types of the file</param>
        /// <param name="Typed">true to treat each column as a data type (int, etc.), False means treat
        /// each file column as varchar</param>
        /// <returns>true if the data in the file is compatible with the server</returns>

        static bool DataWillLoad(List<ProfileColumn> Cols, bool Typed)
        {
            if (Cols == null && !Cfg.Profile)
            {
                Log.InformationMessage("Either no column information, or data was not profiled. Unable to determine compatibility of source data with target table");
                return true; // no profiling so - no ability to make a determination
            }
            Log.InformationMessage("Determining data type compatibility of source data with target table");
            bool WillLoad = ServerUtils.DataWillLoad(Cfg.Server, Cfg.Db, Cfg.Tbl, Cols, Typed, Cfg.Profile);
            if (!WillLoad)
            {
                Log.InformationMessage("Target table {0} cannot be bulk loaded from source data.", Cfg.Tbl);
            }
            else
            {
                Log.InformationMessage("Target table {0} appears to be compatible with source data", Cfg.Tbl);
            }
            return WillLoad;
        }

        /// <summary>
        /// Gets the split columns, if defined. The splitfile can be plain columns, in which case we use the default split
        /// character. (Which can be overridden by the user via the -splitstr command line arg.) The user can also provide
        /// a per-column override split string by appending the column name in the split file with comma, then a split string.
        /// The split string can optionally be quote-enclosed because the space character is sometimes a split string and 
        /// quote-enclosing it makes it easier to see visually. Returns a dictionary in which each entry consists of a key
        /// being the column name to split, and the value being the value embedded in the column value on which to split. 
        /// E.g. say a file looks like:
        ///   COL1,COL2,COL3
        ///   Blue,Red,MD:Maryland
        /// This method would build a dictionary with one entry: key=COL3 value=":"
        /// This dictionary is then used by the file processor to convert the 3-column file into the following 4-column file:
        ///   COL1,COL2,COL3,COL3_DESCR
        ///   Blue,Red,MD,Maryland
        /// </summary>
        /// <returns>The Dictionary, as described. Could be empty. Guaranteed non-null</returns>

        static Dictionary<string, string> GetSplitCols()
        {
            string SplitFQPN = Cfg.Split;
            string SplitStr = Cfg.SplitStr != null ? Cfg.SplitStr : ":";
            string[] Cols = File.ReadAllLines(SplitFQPN);
            Dictionary<string, string> SplitCols = new Dictionary<string, string>();

            foreach (string Col in Cols)
            {
                string[] ColParts = Col.Split(','); // looking for columnname,splitstr or columnname,"splitstr"
                if (ColParts.Length == 1)
                {
                    // did not find a split string so use the default split character
                    SplitCols.Add(Col.Trim(), SplitStr); // no split str in the file so use the class level split str
                }
                else if (ColParts.Length == 2)
                {
                    // use the split str in the file - we don't trim it because it could be the space character
                    // but it could be quote-enclosed so remove the quotes
                    SplitCols.Add(ColParts[0].Trim(), ColParts[1].Replace("\"", string.Empty));
                }
                else
                {
                    throw new Exception(string.Format("Split columns file contains an invalid column specifier: {0}", Col));
                }
            }
            return SplitCols;
        }


        /// <summary>
        /// Removes the prep file
        /// </summary>
        /// <param name="PrepFileFqpn">The path spec of the prep file</param>

        static void RemovePrepFile(string PrepFileFqpn)
        {
            File.Delete(PrepFileFqpn);
        }

        /// <summary>
        /// Generates the frequencies file
        /// </summary>
        /// <param name="Cols">A List of ProfileColumn with frequency information</param>

        static void GenerateFreqs(List<ProfileColumn> Cols)
        {
            Log.InformationMessage("Generating frequencies to frequency file: {0}", Cfg.FreqFile);

            ProfileColumn.SaveFrequencies(Cols, Cfg.FreqFile);

            Log.InformationMessage("Frequency generation completed");
        }

        /// <summary>
        /// Loads the target table
        /// </summary>
        /// <param name="PrepFileFqpn">The path spec of the file to load</param>
        /// <returns></returns>

        static bool LoadTable(string PrepFileFqpn)
        {
            bool result = true;
            try
            {
                string SrcFile = Cfg.File;
                char Delimiter = Cfg.Delimiter;
                int MaxRows = Cfg.MaxRows;
                string TableName = Cfg.Tbl;
                bool SimpleParse = Cfg.SimpleParse;
                int SkipLines = 0;
                string EOFStr = null;

                if (!Cfg.Prep) // file is not being prepped so loader needs to be aware of header, eof, skip, etc
                {
                    SkipLines = Math.Max(Cfg.SkipLines, Cfg.HeaderLine);
                    EOFStr = Cfg.EOFStr;
                }
                else
                {
                    SrcFile = PrepFileFqpn; // load the prepped file
                    SimpleParse = true;
                    Delimiter = '\t'; // prepped files are always tab-delimited
                }

                Log.InformationMessage("Beginning load of table: {0} from source file: {1}", TableName, SrcFile);
                DateTime Start = DateTime.Now;
                if (Cfg.BCP)
                {
                    if (!Cfg.Prep && Cfg.HeaderLine > 0)
                    {
                        Log.WarningMessage("BCP cannot accommodate a header. Attempting to perform the load but - you should use the -prep arg to BCP a file with a header");
                    }
                    ServerUtils.DoBulkInsert(Cfg.Server, Cfg.Db, TableName, SrcFile, MaxRows, Delimiter);
                }
                else
                {
                    ServerUtils.DoBulkLoad(Cfg.Server, Cfg.Db, TableName, SrcFile);
                }
                Log.InformationMessage("Load completed -- Elapsed time (HH:MM:SS.Milli): {0}", DateTime.Now - Start);
            }
            catch (Exception Ex)
            {
                Log.ErrorMessage("An exception occurred attempting to bulk load the table. The exception was: {0}. Stack trace follows:\n{1}", Ex.Message, Ex.StackTrace);
                result = false;
            }
            return result;
        }

        /// <summary>
        /// Truncates the target table
        /// </summary>

        static void TruncateTableOrView()
        {
            ServerUtils.TruncateTableOrView(Cfg.Server, Cfg.Db, Cfg.Tbl);
        }

        /// <summary>
        /// Drops the target table
        /// </summary>

        static void DropTable()
        {
            ServerUtils.DropTable(Cfg.Server, Cfg.Db, Cfg.Tbl);
        }

        /// <summary>
        /// Displays the DDL to the console
        /// </summary>
        /// <param name="Cols">A List of ProfileColumn with column name and data type information</param>

        static void ShowDDL(List<ProfileColumn> Cols)
        {
            string TableName = Cfg.Tbl;
            bool CreateAsVarchar = !Cfg.Typed;
            string DDLStatement = ProfileColumn.GenerateCreateTableStatement(Cols, TableName, CreateAsVarchar);
            Log.InformationMessage("Target table creation DDL statement:\n====================================\n{0}", DDLStatement);
        }

        /// <summary>
        /// Creates the target table
        /// </summary>
        /// <param name="Cols">A List of ProfileColumn with column name and data type information</param>

        static void CreateTable(List<ProfileColumn> Cols)
        {
            string TableName = Cfg.Tbl;
            Log.InformationMessage("Creating table: {0}", TableName);
            bool CreateAsVarchar = !Cfg.Typed;
            string DDLStatement = ProfileColumn.GenerateCreateTableStatement(Cols, TableName, CreateAsVarchar);
            ServerUtils.ExecSql(Cfg.Server, Cfg.Db, DDLStatement);
        }

        /// <summary>
        /// Pre-processes the file (includes profiling). Gets required config info from the global Cfg instance
        /// </summary>
        /// <param name="SplitCols">A Dictionary in which each key is the name of a column and each value is the 
        /// character to split that column on</param>
        /// <param name="PrepFileFqpn">The path spec of the generated prepped file</param>
        /// <returns>A List of ProfileColumn instances with data type and potentially frequency information
        /// generated from the incoming file</returns>

        static List<ProfileColumn> Preprocess(Dictionary<string, string> SplitCols, out string PrepFileFqpn)
        {
            List<ProfileColumn> Cols = null;
            Log.InformationMessage("Begin pre-process");

            Cols = FileProcessor.Process(out PrepFileFqpn, SplitCols);

            Log.InformationMessage("Finish pre-process");
            return Cols;
        }

        /// <summary>
        /// Returns true if the user wants to preview the data file to validate the parser
        /// </summary>
        /// <returns></returns>
        static bool ShouldPreview()
        {
            return Cfg.Preview > 0;
        }

        /// <summary>
        /// Returns true if the prep file should be removed
        /// </summary>

        static bool ShouldRemovePrepFile()
        {
            return Cfg.Prep && !Cfg.PrepRetain;
        }

        /// <summary>
        /// Returns true if the target table should be dropped (and thus created)
        /// </summary>

        static bool ShouldDropTable()
        {
            return Cfg.Drop;
        }

        /// <summary>
        /// Returns true if the target table should be truncated before load
        /// </summary>

        static bool ShouldTruncateTableOrView()
        {
            // even if truncate is specified, there's no reason to truncate if we dropped and re-created
            return Cfg.Truncate && !Cfg.Drop;
        }

        /// <summary>
        /// Returns true if the target table should be created
        /// </summary>
        /// 
        static bool ShouldCreateTable()
        {
            return !ServerUtils.TableOrViewExists(Cfg.Server, Cfg.Db, Cfg.Tbl);
        }

        /// <summary>
        /// Returns True if column splitting is required
        /// </summary>

        static bool ShouldSplitCols()
        {
            return Cfg.Split != null;
        }

        /// <summary>
        /// Returns True if the CREATE TABLE DDL should be displayed
        /// </summary>

        static bool ShouldShowDDL()
        {
            return Cfg.ShowDDL;
        }

        /// <summary>
        /// Returns True if the target table should be loaded
        /// </summary>

        static bool ShouldLoadTable()
        {
            return !Cfg.NoLoad;
        }

        /// <summary>
        /// Returns True if frequencies should be generated
        /// </summary>

        static bool ShouldGenerateFreqs()
        {
            return Cfg.FreqFile != null;
        }

        /// <summary>
        /// Returns true if the utility should profile and/or pre-process the source data
        /// </summary>

        static bool ShouldPreprocess()
        {
            if (Cfg.Profile)
            {
                // user explicitly specified profiling
                return true;
            }
            bool TblOrViewExists = ServerUtils.TableOrViewExists(Cfg.Server, Cfg.Db, Cfg.Tbl);
            if (!TblOrViewExists)
            {
                // target table/view does not exist, must be created - therefore need to profile to
                // determine column data types
                return true;
            }
            if (TblOrViewExists && Cfg.Drop)
            {
                // user wants to drop an existing table/view - therefore need to profile to
                // determine column data types
                return true;
            }
            if (Cfg.Prep)
            {
                // user explicitly wants to prep the source file
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parses the command line args
        /// </summary>
        /// <param name="args">from .Net</param>
        /// <returns>true if the args parsed ok, else false. If false, prints the usage instructions</returns>

        static bool ParseArgs(string[] args)
        {
            if (!AppSettingsImpl.Parse(SettingsSource.CommandLine, args))
            {
                if (AppSettingsBase.ParseErrorMessage != null)
                {
                    Console.WriteLine(AppSettingsBase.ParseErrorMessage);
                }
                else
                {
                    AppSettingsBase.ShowUsage();
                }
                return false;
            }
            return true;
        }
    }
}
