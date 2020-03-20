using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static load_file.Globals;

namespace load_file
{
    /// <summary>
    /// The data types supported by the class
    /// </summary>

    enum ParseType
    {
        /// <summary>
        /// SQL INT, BIGINT, NUMERIC(n,n)
        /// </summary>
        NUMERIC,
        /// <summary>
        /// SQL DATETIME
        /// </summary>
        DATETIME,
        /// <summary>
        /// SQL VARCHAR(n)
        /// </summary>
        CHARACTER,
        /// <summary>
        /// Not analyzed yet
        /// </summary>
        UNINITIALIZED
    };

    /// <summary>
    /// Contains functionality to profile data
    /// </summary>

    class ProfileColumn
    {
        public string ColName { get; private set; }
        public int SplitOrdinal { get; private set; }
        public string SplitStr { get; private set; }

        private int MaxLen = 0;
        private Dictionary<string, int> Frequencies = new Dictionary<string, int>();
        private int ParsesBigInt = 0;
        private int ParsesInt = 0;
        private int ParsesDecimal = 0;
        private int ParsesDateTime = 0;
        private int ParsesCharacter = 0;
        private int RowCnt = 0;
        private int Scale = 0;
        private int Precision = 0;
        private ParseType LastParseType = ParseType.UNINITIALIZED;

        private static DateTime MinSQLDate = new DateTime(1763, 1, 1);

        /// <summary>
        /// Splits the passed column list using the passed SplitCols dictionary. A column "foo" - if designated to split - 
        /// gets a second inserted into the list immediately to its right named "foo_descr". This is a capability for
        /// pre-splitting code/description columns in an input file to avoid the cost of doing it in the database after
        /// the data has been bulk imported. It is more performant to do it in the file as a pre-processing step.
        /// </summary>
        /// <param name="Cols">The columns to split</param>
        /// <param name="SplitCols">The split definition. The key is the column name and the value
        /// is the split string</param>
        /// <returns>The split column list</returns>

        public static List<ProfileColumn> SplitColumnList(List<ProfileColumn> Cols, Dictionary<string, string> SplitCols)
        {
            List<ProfileColumn> NewCols = new List<ProfileColumn>();
            int SplitCount = 0;
            for (int FldNum = 0; FldNum < Cols.Count; ++FldNum)
            {
                NewCols.Add(Cols[FldNum]);
                if (SplitCols.ContainsKey(Cols[FldNum].ColName))
                {
                    ++SplitCount;
                    NewCols.Add(new ProfileColumn(Cols[FldNum].ColName + "_descr"));
                    Cols[FldNum].SplitStr = SplitCols[Cols[FldNum].ColName]; // value from the dictionary is the split string
                    Cols[FldNum].SplitOrdinal = FldNum;
                }
            }
            if (SplitCount == 0)
            {
                throw new LoadException("A split file was specified, but the utility did not find any matching columns to split");
            }
            return NewCols;
        }

        /// <summary>
        /// Splits the passed data field list using the passed list of ordinals, which has been initialized
        /// with split configuration info. This method assumes splitting is to occur and doesn't check first so - don't
        /// call this if no splitting is required.
        /// </summary>
        /// <param name="InLine">A list of fields. E.g. "x", "y", "code:descr" might be 
        /// returned as "x", "y", "code", "descr"</param>
        /// <param name="SplitOrdinals">A dictionary in which each key is the ordinal position of a field that is
        /// to be split, and the value is the character to split on. Folling the example for the InLine arg, the
        /// Dictionary would look like: <2,":"> </param>
        /// <returns>A List of strings, post-splitting. Non split fields are copied as is. Split fields are trimmed.</returns>

        public static List<string> SplitFields(List<string> InLine, Dictionary<int, string> SplitOrdinals)
        {
            List<string> SplitLine = new List<string>();
            for (int FldNum = 0; FldNum < InLine.Count; ++FldNum)
            {
                if (SplitOrdinals.ContainsKey(FldNum))
                {
                    SplitLine.Add(SplitLeft(InLine[FldNum], SplitOrdinals[FldNum]));
                    SplitLine.Add(SplitRight(InLine[FldNum], SplitOrdinals[FldNum]));
                }
                else
                {
                    SplitLine.Add(InLine[FldNum]);
                }
            }
            return SplitLine;
        }

        /// <summary>
        /// Splits a code description field (like "MD:MARYLAND") and returns everything to the
        /// left of the first ocurrence of split string ("MD" in the example).
        /// </summary>
        /// <param name="ToSplit">The string to split</param>
        /// <param name="SplitOn">The string to split on</param>
        /// <returns>The left side. If the string to split does not contain any
        /// occurrences of the string to split on, then the string to split is returned as is
        /// </returns>

        private static string SplitLeft(string ToSplit, string SplitOn)
        {
            return ToSplit.IndexOf(SplitOn) == -1 ? ToSplit : ToSplit.Substring(0, ToSplit.IndexOf(SplitOn)).Trim();
        }

        /// <summary>
        /// Splits a code description field (like "MD:MARYLAND") and returns everything to the
        /// right of the first ocurrence of split string ("MARYLAND" in the example)
        /// </summary>
        /// <param name="ToSplit">The string to split</param>
        /// <param name="SplitOn">The string to split on</param>
        /// <returns>The right side. If the string to split does not contain any
        /// occurrences of the string to split on, then the string to split is returned as is
        /// </returns>

        private static string SplitRight(string ToSplit, string SplitOn)
        {
            return ToSplit.IndexOf(SplitOn) == -1 ? string.Empty : ToSplit.Substring(ToSplit.IndexOf(SplitOn) + SplitOn.Length).Trim();
        }

        /// <summary>
        /// Builds a List of ProfileColumn instances from the input file, or from the column list file, depending
        /// on configuration. (Uses the global Cfg class.)
        /// </summary>
        /// <param name="SplitCols">The split definition. The key is the column name and the value
        /// is the split string. If empty, then no splitting is performed.</param>
        /// <returns></returns>

        public static List<ProfileColumn> BuildColumnList(Dictionary<string, string> SplitCols)
        {
            List<ProfileColumn> ColsToReturn = null;

            if (Cfg.ColFile != null)
            {
                Log.InformationMessage("Generating column names from the column names file: {0}", Cfg.ColFile);
                string[] cols = File.ReadAllLines(Cfg.ColFile);
                ColsToReturn = MakeColList(cols);
            }
            else
            {
                Log.InformationMessage("Generating column names from input file");
                List<string> cols = null;
                int RowsRead = 0;
                using (StreamReader Rdr = new StreamReader(Cfg.File))
                {
                    string InLine;
                    while ((InLine = Rdr.ReadLine()) != null)
                    {
                        ++RowsRead; // 1-relative in this loop
                        if (Cfg.HeaderLine > 0 && RowsRead == Cfg.HeaderLine)
                        {
                            ColsToReturn = MakeColList(SplitInputLine(InLine));
                            break;
                        }
                        else if (Cfg.SkipLines > 0 && RowsRead <= Cfg.SkipLines)
                        {
                            continue;
                        }
                        cols = SplitInputLine(InLine); // take first data row
                        List<string> cols2 = new List<string>();
                        for (int i = 0; i < cols.Count; ++i)
                        {
                            cols2.Add(string.Format("col{0}", i));
                        }
                        ColsToReturn = MakeColList(cols2);
                        break;
                    }
                }
            }
            if (SplitCols != null)
            {
                int OriginalColCount = ColsToReturn.Count;
                ColsToReturn = SplitColumnList(ColsToReturn, SplitCols);
                Log.InformationMessage("Column names generated. Original column count: {0}. Split column count: {1}", OriginalColCount, ColsToReturn.Count);
            }
            else
            {
                Log.InformationMessage("Column names generated. Column count: {0}", ColsToReturn.Count);
            }
            return ColsToReturn;
        }

        /// <summary>
        /// Split an input line on the delimeter
        /// </summary>
        /// <remarks>
        /// Input line can have quoted, or non-quoted fields. Both are handled. MUST be delimiter-separated though.
        /// </remarks>
        /// <param name="InputLine"></param>
        /// <param name="Delimiter"></param>
        /// <returns></returns>
        /// 
        public static List<string> SplitInputLine(string InputLine)
        {
            if (Cfg.Fixed.IsNullOrEmpty())
            {
                DelimitedLineParser Parser = new DelimitedLineParser(Cfg.Delimiter, Cfg.SimpleParse);
                return Parser.SplitLine(InputLine, true);
            }
            else
            {
                FixedWidthReader Rdr = new FixedWidthReader();
                return Rdr.ParseLine(InputLine);
            }
        }

        /// <summary>
        /// Determines whether the data in this instance is compatible with the passed server column data type
        /// </summary>
        /// <remarks>Will load numeric types into varchar fields if the varchar will hold the field contents</remarks>
        /// <remark>Note - only checks for the types that it assigns when profiling</remark>
        /// <param name="ServerCol">a SQL Server Column instance that this instance is potentially going to be loaded into</param>
        /// <param name="Typed">True if the comparison is on data type. If false, then if the server column is a character type, the
        /// method compares the lengths. If false and the server type is not a character type, then the method returns false.</param>
        /// <returns></returns>

        public bool WillLoad(Column ServerCol, bool Typed)
        {
            DataType ThisDataType = SQLNativeDataType();
            const int VARCHAR_MAX = -1;

            if (!Typed) // then treat everything as varchar
            {
                return ServerCol.DataType.Name.In("char", "varchar", "nchar", "nvarchar", "ntext", "text") &&
                    (ServerCol.DataType.MaximumLength == VARCHAR_MAX || ServerCol.DataType.MaximumLength >= ThisDataType.MaximumLength);
            }
            else if (ThisDataType == null)
            {
                // the file did not contain any data in this column so this column is compatible with any data type
                return true;
            }
            else if (ThisDataType.Name == "int")
            {
                return
                    ServerCol.DataType.Name.In("int", "bigint") ||

                    (ServerCol.DataType.Name.In("numeric", "decimal") &&
                        ServerCol.DataType.NumericPrecision - ServerCol.DataType.NumericScale >= 10) ||

                    (ServerCol.DataType.Name.In("char", "varchar", "nchar", "nvarchar", "ntext", "text") &&
                        (ServerCol.DataType.MaximumLength == VARCHAR_MAX ||
                        ServerCol.DataType.MaximumLength >= ThisDataType.MaximumLength));
            }
            else if (ThisDataType.Name == "bigint")
            {
                return 
                    ServerCol.DataType.Name.In("bigint") ||

                    (ServerCol.DataType.Name.In("numeric", "decimal") &&
                    ServerCol.DataType.NumericPrecision - ServerCol.DataType.NumericScale >= 19) ||

                    (ServerCol.DataType.Name.In("char", "varchar", "nchar", "nvarchar", "ntext", "text") &&
                        (ServerCol.DataType.MaximumLength == VARCHAR_MAX ||
                        ServerCol.DataType.MaximumLength >= ThisDataType.MaximumLength));
            }
            else if (ThisDataType.Name.In("numeric"))
            {
                return
                    (ServerCol.DataType.Name.In("numeric", "decimal") &&
                    (ServerCol.DataType.NumericPrecision >= ThisDataType.NumericPrecision &&
                        ServerCol.DataType.NumericScale >= ThisDataType.NumericScale)) ||

                    (ServerCol.DataType.Name.In("char", "varchar", "nchar", "nvarchar", "ntext", "text") &&
                        (ServerCol.DataType.MaximumLength == VARCHAR_MAX ||
                        ServerCol.DataType.MaximumLength >= ThisDataType.MaximumLength));
            }
            else if (ThisDataType.Name.In("datetime"))
            {
                return ServerCol.DataType.Name.In("datetime") ||

                (ServerCol.DataType.Name.In("char", "varchar", "nchar", "nvarchar", "ntext", "text") &&
                    (ServerCol.DataType.MaximumLength == VARCHAR_MAX ||
                    ServerCol.DataType.MaximumLength >= ThisDataType.MaximumLength));
            }
            else if (ThisDataType.Name.In("varchar"))
            {
                return ServerCol.DataType.Name.In("char", "varchar", "nchar", "nvarchar", "ntext", "text") &&
                    (ServerCol.DataType.MaximumLength == VARCHAR_MAX ||
                    ServerCol.DataType.MaximumLength >= ThisDataType.MaximumLength);
            }
            return false;
        }

        /// <summary>
        /// Translate the raw colunm name into a valid SQL Server identifier
        /// </summary>
        /// <returns></returns>

        public string SQLColname()
        {
            string SQLColname = ColName;
            // Column names can contain any valid characters (for example, spaces). If column names contain any characters 
            // except letters, numbers, and underscores, the name must be delimited (we use brackets here.) Also - column
            // names can't start with a number. If the column name starts with a number - that's also invalid but in that
            // case, prefix the column with an underscore
            if (new Regex("[^0-9a-zA-Z_]").Matches(SQLColname.Substring(0)).Count != 0)
            {
                SQLColname = "[" + SQLColname + "]";
            }
            else if ("0123456789".Contains(ColName[0]))
            {
                SQLColname = "_" + SQLColname;
            }
            return SQLColname;

            // TODO - option to cleanse name
            //// first do some smart replacements
            //SQLColname = SQLColname.Replace("(", "");
            //SQLColname = SQLColname.Replace(")", "");
            //SQLColname = SQLColname.Replace("%", "percent");
            //SQLColname = SQLColname.Replace(" ", "_");
            //SQLColname = SQLColname.Replace("-", "_");
            //SQLColname = SQLColname.Replace("&", "and");
            //// then brute force everything else

            //Regex r = new Regex("[a-zA-Z_@#]");
            //if (r.Matches(SQLColname.Substring(0)).Count == 0)
            //{
            //    SQLColname = "_" + SQLColname;
            //}
            //foreach (Match m in Regex.Matches(SQLColname, "[^0-9a-zA-Z_@#]"))
            //{
            //    SQLColname = SQLColname.Replace(m.Value, "_");
            //}
            //return SQLColname;
        }

        /// <summary>
        /// Returns the Column Name for the class
        /// </summary>
        /// <returns></returns>

        public override string ToString()
        {
            return ColName;
        }

        /// <summary>
        /// Creates a List of ProfileColumn instances from the passed enumerable column names
        /// </summary>
        /// <param name="ColNames">an enumerable of column names</param>
        /// <returns>The List</returns>
        public static List<ProfileColumn> MakeColList(IEnumerable<string> ColNames)
        {
            List<ProfileColumn> ColList = new List<ProfileColumn>();
            foreach (string ColName in ColNames)
            {
                ColList.Add(new ProfileColumn(ColName.Trim()));
            }
            return ColList;
        }

        /// <summary>
        /// Constructs an instance from the passed column name
        /// </summary>
        /// <param name="ColName">The column name</param>
        public ProfileColumn(string ColName)
        {
            this.ColName = ColName;
            SplitOrdinal = -1; // will never match a column ordinal
        }

        /// <summary>
        /// Profiles the data in this instance based on the passed value
        /// </summary>
        /// <param name="val">The value to profile</param>
        /// <param name="DoFreqs">True to generate frequencies (takes more time, consumes memory)</param>
        /// <param name="Typed">True to figure out the data type. If false, then only the max length is
        /// calculated - which is considerably faster</param>

        public void Profile(string val, bool DoFreqs, bool Typed)
        {
            if (MaxLen < val.Length)
            {
                MaxLen = val.Length;
            }
            if (DoFreqs)
            {
                CalcFreqs(val);     // frequencies
            }
            if (Typed)
            {
                CalcDataType(val);  // determine a SQL Server data type that will support the column
            }
        }

        /// <summary>
        /// Calculates frequencies for the column. If > 1000 values, then stops there. "HELLO" and "hello" are the same value
        /// </summary>
        /// <param name="val">The value to frequency</param>

        private void CalcFreqs(string val)
        {
            val = val.ToLower();
            if (Frequencies.Keys.Contains(val))
            {
                Frequencies[val] = Frequencies[val] + 1;
            }
            // if more than 1000 than it's probably not a lookup
            else if (Frequencies.Count < 1000)
            {
                Frequencies.Add(val, 1);
            }
        }

        /// <summary>
        /// Determines the data type. As successively more and more values are profiled, the data type will converge to
        /// one that is guaranteed to support all the data in that column for the entire profiled file.
        /// </summary>
        /// <param name="val">The value to inspect</param>

        private void CalcDataType(string val)
        {
            if (val.Length == 0)
            {
                return;
            }

            // inrement the total number of non-empty values we have examined. The final type determination uses this

            ++RowCnt;

            if (ParsesCharacter > 0 || val.Length > 30)
            {
                // if the string is longer than 30 chars it probably won't parse as any of the types below,
                // so save the processing time. And - once a field parses as character (i.e. if failed to parse as any of
                // the other types) then it's character thenceforward and we don't waste time trying to parse it any more
                ++ParsesCharacter;
                LastParseType = ParseType.CHARACTER;
                return;
            }

            bool FailedNumeric = false, FailedDateTime = false;

            // as an optimization, if we had a successful non-character parse last time, try the same type
            // this time, assuming that the columns are of uniform type. With a clean data set, we will only
            // perform the same parse on a column, saving time.

            if (LastParseType != ParseType.UNINITIALIZED)
            {
                switch (LastParseType)
                {
                    case ParseType.NUMERIC:
                        if (ParsesAsNumeric(val)) return;
                        FailedNumeric = true;
                        break;
                    case ParseType.DATETIME:
                        if (ParsesAsDate(val)) return;
                        FailedDateTime = true;
                        break;
                }
            }

            // as soon as we get a matching data type we stop to save time. If we already failed a parse above
            // don't re-try it here

            if (!FailedNumeric && WillParseNumeric(val))
            {
                if (ParsesAsNumeric(val))
                {
                    return;
                }
            }
            if (!FailedDateTime && ParsesAsDate(val))
            {
                return;
            }
            ++ParsesCharacter;
            LastParseType = ParseType.CHARACTER;
        }

        /// <summary>
        /// Attempts to parse the value as one of the supported numeric types.
        /// </summary>
        /// <param name="val">the value to parse</param>
        /// <returns>True if it parses. If it does parse, them the appropriate member fields are updated</returns>

        private bool ParsesAsNumeric(string val)
        {
            int IntOut = -1;
            if (int.TryParse(val, out IntOut))
            {
                ++ParsesInt;
            }
            long BigIntOut = -1;
            if (long.TryParse(val, out BigIntOut))
            {
                ++ParsesBigInt;
            }
            decimal DecimalOut = -1;
            if (decimal.TryParse(val, out DecimalOut))
            {
                ++ParsesDecimal;
                // keep track of the maximum range and precision
                int TmpPrecision = DecimalOut.Precision();
                int TmpScale = DecimalOut.Scale();
                if (Precision < TmpPrecision)
                {
                    Precision = TmpPrecision;
                }
                if (Scale < TmpScale)
                {
                    Scale = TmpScale;
                }
            }
            if (IntOut != -1 || BigIntOut != -1 || DecimalOut != -1)
            {
                LastParseType = ParseType.NUMERIC;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to parse the value as a date
        /// </summary>
        /// <param name="val">the value to parse</param>
        /// <returns>True if it parses. If it does parse, them the appropriate member fields are updated</returns>

        private bool ParsesAsDate(string val)
        {
            DateTime DateOut;
            if (DateTime.TryParse(val, out DateOut))
            {
                // SQL Server datetime range is: 1753-01-01 through 9999-12-31
                // C# DateTime.MinValue =  January 1, 0001 and MaxValue is December 31, 9999
                if (DateTime.Compare(DateOut, MinSQLDate) >= 0) // value in the data is >= SQL Server minimum
                {
                    ++ParsesDateTime;
                    LastParseType = ParseType.DATETIME;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A performance optimization. Looks at the passed value and returns false as soon as a character
        /// is encountered that would not parse as a numeric value.
        /// </summary>
        /// <param name="val">The value to inspect</param>
        /// <returns>True if the value will parse as a numeric. E.g. only contains numbers, and comma, period, or minus
        /// sign</returns>
        static bool WillParseNumeric(string val)
        {
            for (int i = 0; i < val.Length; ++i)
            {
                if (!"1234567890-,.".Contains(val[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Using the Maximum length of the instance, pad it to some even numbers to provide some wiggle room
        /// on the server data for VARCHAR types. E.g. rather than create a column that is VARCHAR(43), create
        /// a column that is VARCHAR(50)
        /// </summary>
        /// <returns>The padded length as a string that can be used in a VARCHAR(x) data type specifier</returns>

        public string PadLength()
        {
            int col_len = (int)((float)MaxLen * 1.6f); // increase size by 60%
            if (col_len < 20) return "20";
            if (col_len < 50) return "50";
            if (col_len < 100) return "100";
            if (col_len < 255) return "255";
            if (col_len < 500) return "500";
            if (col_len < 1000) return "1000";
            if (col_len < 2000) return "2000";
            if (col_len < 3000) return "3000";
            if (col_len < 4000) return "4000";
            return "max";
        }

        /// <summary>
        /// Saves the frequences out to a text file
        /// </summary>
        /// <param name="Cols">The List of ProfileColumn instances containing the frequencies</param>
        /// <param name="FileName">The path specifier to write to. The file is replaced if it exists.</param>

        public static void SaveFrequencies(List<ProfileColumn> Cols, string FileName)
        {
            using (StreamWriter sw = new StreamWriter(FileName))
            {
                foreach (ProfileColumn Col in Cols)
                {
                    List<string> Keys = Col.Frequencies.Keys.ToList();
                    Keys.Sort();
                    string ColHeader = string.Format("FLD: {0} ({1})", Col.ColName, Col.Frequencies.Count);
                    sw.WriteLine(string.Format("{0}\n{1}\nValue\tCount\n=====\t=====", ColHeader, ColHeader.Stuff('=')));
                    foreach (string Key in Keys)
                    {
                        sw.WriteLine(string.Format("{0}\t{1}", Key, Col.Frequencies[Key]));
                    }
                    sw.WriteLine();
                }
            }
        }

        /// <summary>
        /// Generates a create table statement using the passed arguments
        /// </summary>
        /// <param name="Cols">All the column name and data type information</param>
        /// <param name="TableName">The table name</param>
        /// <param name="CreateAsVarchar">TRUE to create all columns as VARCHAR(n) with a length sufficient
        /// to hold the data. False to create as INT, BIGINT, NUMERIC, or VARCHAR based on the data profile of
        /// each column.</param>
        /// <returns></returns>

        public static string GenerateCreateTableStatement(List<ProfileColumn> Cols, string TableName, bool CreateAsVarchar)
        {
            return CreateAsVarchar ? VarCharDDL(Cols, TableName) : TypedDDL(Cols, TableName);
        }

        /// <summary>
        /// Generates a create table statement with typed column names based on the profiled data
        /// </summary>
        /// <param name="Cols">A List of ProfileColumn instances with data type information</param>
        /// <param name="TableName">The table name</param>
        /// <returns>A CREATE TABLE statement</returns>

        private static string TypedDDL(List<ProfileColumn> Cols, string TableName)
        {
            string FmtString = "    {0}{1,-" + MaxColNameLength(Cols) + "} {2}\n";
            int ColNum = 0;
            string DDL = "create table " + TableName + "\n(\n";

            foreach (ProfileColumn Col in Cols)
            {
                string DataType = Col.SQLDataType() == null ? "varchar(max)" : Col.SQLDataType();
                DDL += string.Format(FmtString, ColNum++ == 0 ? " " : ",", Col.SQLColname().ToLower(), DataType);
            }
            return DDL + ");";
        }

        /// <summary>
        /// Generates a create table statement with VARCHAR column names based on the profiled data
        /// </summary>
        /// <param name="Cols">A List of ProfileColumn instances with data type information</param>
        /// <param name="TableName">The table name</param>
        /// <returns>A CREATE TABLE statement</returns>

        public static string VarCharDDL(List<ProfileColumn> Cols, string TableName)
        {
            string FmtString = "    {0}{1,-" + MaxColNameLength(Cols) + "} varchar({2})\n";
            int ColNum = 0;
            string DDL = "create table " + TableName + "\n(\n";

            foreach (ProfileColumn Col in Cols)
            {
                DDL += string.Format(FmtString, ColNum++ == 0 ? " " : ",", Col.SQLColname().ToLower(), Col.PadLength());
            }
            return DDL + ");";
        }

        /// <summary>
        /// Determines the longest column name in the passed list. Used by the caller to format the DDL
        /// so that the data types align at the same column number in the DDL. E.g.:
        ///    column1                     varchar(1)
        ///    naics                       varchar(20)
        ///    thisisareallylongcolumnname int
        /// </summary>
        /// <param name="Cols"></param>
        /// <returns>The length of the longest column name in the passed collection</returns>

        private static int MaxColNameLength(List<ProfileColumn> Cols)
        {
            int MaxColNameLen = -1;
            Cols.ForEach(Col => MaxColNameLen = MaxColNameLen < Col.ColName.Length ? Col.ColName.Length : MaxColNameLen);
            return MaxColNameLen;
        }

        /// <summary>
        /// Translates the data type of the instance into a DataType instance so that the data type
        /// can be compared with a SQL Server data type. If the column contained no data (hence cannot be determined to be
        /// any given data type) then returns null.
        /// </summary>
        /// <returns>The SMO DataType instance that matches the data of this instance, or null if the column did not 
        /// contain any data</returns>
        private DataType SQLNativeDataType()
        {
            string Dt = SQLDataType();
            if (Dt == null)
            {
                // profiling was not enabled or the column contained no data
                return DataType.VarCharMax;
            }
            if (Dt == "int")
            {
                return DataType.Int;
            }
            if (Dt == "bigint")
            {
                return DataType.BigInt;
            }
            if (Dt.StartsWith("numeric"))
            {
                return DataType.Numeric(Scale, Precision);
            }
            if (Dt == "datetime")
            {
                return DataType.DateTime;
            }
            if (Dt == "varchar(max)")
            {
                return DataType.VarCharMax;
            }
            if (Dt.StartsWith("varchar"))
            {
                return DataType.VarChar(MaxLen);
            }
            throw new LoadException(string.Format("Unsupported data type for compatibility determination: {0}", Dt));
        }

        /// <summary>
        /// Gets the appropriate SQL data type based on the profiled data. If the profiled column contained no data,
        /// or profiling was not performed and hence there is no way to determine the data type, then returns null,
        /// and the caller must decide how to handle that condition.
        /// </summary>
        /// <param name="Padded">True to pad VARCHARS, else False</param>
        /// <returns>the SQL data type - e.g. "INT", "NUMERIC(12,2)", "VARCHAR(3)", "DATETIME", etc. Returns null
        /// if the coumn contained no data</returns>

        public string SQLDataType(bool Padded = true)
        {
            float DtPct = (float)ParsesDateTime / (float)RowCnt;
            float IntPct = (float)ParsesInt / (float)RowCnt;
            float BigIntPct = (float)ParsesBigInt / (float)RowCnt;
            float DecimalPct = (float)ParsesDecimal / (float)RowCnt;

            string DataType = null;

            if (IntPct == 1)
            {
                DataType = "int";
            }
            else if (BigIntPct == 1)
            {
                DataType = "bigint";
            }
            else if (DecimalPct == 1)
            {
                if (Precision == 0 && Scale == 0)
                {
                    // special case where all the values profiled were like "0.0" so there is no precision or scale
                    DataType = string.Format("numeric(1,0)"); // just make up a reasonable data type
                }
                else
                {
                    DataType = string.Format("numeric({0},{1})", Precision, Scale);
                }
            }
            else if (DtPct == 1)
            {
                DataType = "datetime";
            }
            else if (MaxLen > 0) // if MaxLen==0, then null will be returned by the method
            {
                DataType = string.Format("varchar({0})", Padded ? PadLength() : MaxLen.ToString());
            }
            return DataType;
        }
    }
}
