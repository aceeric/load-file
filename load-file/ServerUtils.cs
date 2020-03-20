using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System;
using System.IO;
using static load_file.Globals;

namespace load_file
{
    /// <summary>
    /// Methods that support interacting with the database server
    /// </summary>
    class ServerUtils
    {
        /// <summary>
        /// Determines whether the data in the file will load into an existing database table/view
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableOrViewName">The table or view</param>
        /// <param name="FileCols">A List of ProfileColumn instances with type information from the file</param>
        /// <param name="Typed">True if the comarison is based on data type. Otherwise just a varchar comparison</param>
        /// <param name="Profile">True if the data was profiled, and hence the ProfileColumn instances contain
        /// data type information. If false, then the only check performed is if the number of columns match</param>
        /// <returns>True if the table will load, else false</returns>

        public static bool DataWillLoad(string ServerName, string DatabaseName, string TableOrViewName, List<ProfileColumn> FileCols, bool Typed,
            bool Profile)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                List<Column> ServerCols = new List<Column>();
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = "dbo";
                if (TableOrViewName.Contains("."))
                {
                    string[] tmp = TableOrViewName.Split('.');
                    SchemaName = tmp[0];
                    TableOrViewName = tmp[1];
                }
                if (Db.Tables.Contains(TableOrViewName, SchemaName))
                {
                    Table TargetTable = Db.Tables[TableOrViewName, SchemaName];
                    foreach (Column SqlCol in TargetTable.Columns)
                    {
                        ServerCols.Add(SqlCol);
                    }
                }
                else if (Db.Views.Contains(TableOrViewName, SchemaName))
                {
                    View TargetView = Db.Views[TableOrViewName, SchemaName];
                    foreach (Column SqlCol in TargetView.Columns)
                    {
                        ServerCols.Add(SqlCol);
                    }
                }
                else
                {
                    throw new LoadException(string.Format("Specified view or table does not exist: {0}", TableOrViewName));
                }
                if (ServerCols.Count != FileCols.Count)
                {
                    Log.InformationMessage("Incompatible column count: source data has {0} columns but target table has {1} columns.",
                        FileCols.Count, ServerCols.Count);
                    return false;
                }
                if (!Profile)
                {
                    // if the data was not profiled then there is no data type information to compare to the server
                    return true;
                }
                return WillLoad(ServerCols, FileCols, Typed);
            }
        }

        /// <summary>
        /// Determines whether the list of server columns is compatible with the data in the list of ProfileColumn columns.
        /// The comparison is based on ordinal position
        /// </summary>
        /// <param name="ServerCols">The columns from the table</param>
        /// <param name="FileCols">The ProfileColumn List containnig data type info from the file</param>
        /// <returns>True if the file columns can be written to the server columns</returns>

        private static bool WillLoad(List<Column> ServerCols, List<ProfileColumn> FileCols, bool Typed)
        {
            bool RetVal = true;
            int ErrCount = 0;
            for (int i = 0; i < ServerCols.Count; ++i)
            {
                if (!FileCols[i].WillLoad(ServerCols[i], Typed))
                {
                    if (ErrCount++ == 0)
                    {
                        Log.InformationMessage("Data Type incompatibilities exist:");
                    }
                    Log.InformationMessage("At ordinal position {0}, column {1} {2} in input file will not load into column {3} {4} in database table",
                        i, FileCols[i].ColName, FileCols[i].SQLDataType(false), ServerCols[i].Name, XlatDatatype(ServerCols[i]));
                    RetVal = false;
                }
            }
            return RetVal;
        }

        /// <summary>
        /// Translate the data type in the passed server column to a string representation. E.g. "varchar(100)"
        /// </summary>
        /// <param name="Col">The server column</param>
        /// <returns>The string representation of the data type</returns>

        private static string XlatDatatype(Column Col)
        {
            if (Col.Computed)
            {
                return "as " + Col.ComputedText.Replace("[", string.Empty).Replace("]", string.Empty);
            }

            DataType Dt = Col.DataType;
            string DtName = Dt.Name.ToLower();
            if (DtName == "varchar" || DtName == "char" || DtName == "varbinary" ||
                DtName == "nvarchar" || DtName == "nchar" || DtName == "nvarbinary")
            {
                return Dt.Name + "(" + (Dt.MaximumLength == -1 ? "max" : Dt.MaximumLength.ToString()) + ")";
            }

            if (DtName == "decimal" || DtName == "numeric")
            {
                return Dt.Name + "(" + Dt.NumericPrecision + ", " + Dt.NumericScale + ")";
            }

            if (DtName == "date" || DtName == "datetime" || DtName == "money" || DtName == "int" || DtName == "bigint"
                || DtName == "bit" || DtName == "tinyint" || DtName == "float" || DtName == "smallint" || DtName == "text"
                || DtName == "uniqueidentifier" || DtName == "ntext")
            {
                return Dt.Name;
            }

            throw new Exception("Unhandled data type: " + Dt.Name);
        }

        /// <summary>
        /// Loads the passed file into the server using a SQL BULK INSERT statement
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableName">The table</param>
        /// <param name="SourceFile">The source file to load</param>
        /// <param name="MaxRows">A row throttle</param>
        /// <param name="Delimiter">The delimiter in the source file</param>

        public static void DoBulkInsert(string ServerName, string DatabaseName, string TableName, string SourceFile, int MaxRows, char Delimiter)
        {
            Log.InformationMessage("Started Bulk Insert");
            string Sql = string.Format(
               "bulk insert {0}.{1} " +
               "from '{2}' " +
               "with (datafiletype = 'char', fieldterminator = '{3}', batchsize = 10000, tablock, lastrow={4})", 
               DatabaseName, TableName, Path.GetFullPath(SourceFile),
               Delimiter.Xlat(new char[] { '\t', '|', ',' }, new string[] { "\\t", "|", "," }), MaxRows);
            ExecStatement(ServerName, DatabaseName, Sql);
            Log.InformationMessage("Completed Bulk Insert");
        }

        /// <summary>
        /// Loads the passed file into the server using a SQL BULK INSERT statement
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableName">The table</param>
        /// <param name="SourceFile">The source file to load</param>

        public static void DoBulkLoad(string ServerName, string DatabaseName, string TableName, string SourceFile)
        {
            Log.InformationMessage("Started SqlBulkCopy");
            int InRows = 0;
            using (SqlConnection Connection = GetSqlConnection(ServerName, DatabaseName))
            using (SqlBulkCopy Bc = new SqlBulkCopy(Connection, SqlBulkCopyOptions.TableLock, null))
            using (DataTable Tbl = new DataTable())
            {
                Bc.DestinationTableName = TableName;
                Connection.Open();
                using (FileReader Rdr = FileReader.NewFileReader(SourceFile, false))
                {
                    List<string> InFields = null;
                    while ((InFields = Rdr.ReadLine()) != null)
                    {
                        ++InRows;
                        if (InRows == 1)
                        {
                            InitSqlBulkCopyColMappings(Bc, InFields);
                            InitDataTable(Tbl, InFields);
                        }
                        LoadDataTable(Tbl, InFields);
                        if (InRows % 10000 == 0)
                        {
                            Log.InformationMessage("SqlBulkCopy {0} rows", InRows); // something to look at for large files
                            Bc.BatchSize = Tbl.Rows.Count;
                            Bc.WriteToServer(Tbl);
                            Tbl.Clear();
                        }
                    }
                    if (Tbl.Rows.Count != 0) // pick up the remainder
                    {
                        Bc.BatchSize = Tbl.Rows.Count;
                        Bc.WriteToServer(Tbl);
                    }
                }
            }
            Log.InformationMessage("Completed SqlBulkCopy. Loaded {0} rows", InRows);
        }

        /// <summary>
        /// Initializes the column mappings of the passed SqlBulkCopy from the passed list of fields
        /// </summary>
        /// <param name="Bc">SqlBulkCopy instance to initialize</param>
        /// <param name="InFields">Fields from the source file to create column mappings for</param>

        private static void InitSqlBulkCopyColMappings(SqlBulkCopy Bc, List<string> InFields)
        {
            for (int i = 0; i < InFields.Count; ++i)
            {
                Bc.ColumnMappings.Add(i, i);
            }
        }

        /// <summary>
        /// Initializes the columns of the passed DataTable from the passed list of fields
        /// </summary>
        /// <param name="Bc">DataTable instance to initialize</param>
        /// <param name="InFields">Fields from the source file to create column mappings for</param>

        private static void InitDataTable(DataTable Dt, List<string> InFields)
        {
            foreach (string Fld in InFields)
            {
                Dt.Columns.Add(new DataColumn());
            }
        }

        /// <summary>
        /// Loads the passed DataTable with one new row of data from the InFields List
        /// </summary>
        /// <param name="Tbl">The DataTable to add a row to</param>
        /// <param name="InFields">A list of string fields comprising the row to add</param>

        private static void LoadDataTable(DataTable Tbl, List<string> InFields)
        {
            int Col;
            DataRow NewRow = Tbl.NewRow();
            Col = 0;
            foreach (string Fld in InFields)
            {
                if (string.IsNullOrEmpty(Fld))
                {
                    NewRow[Col] = DBNull.Value;
                }
                else
                {
                    NewRow[Col] = Fld;
                }
                Col++;
            }
            Tbl.Rows.Add(NewRow);
        }

        /// <summary>
        /// Executes the passed statement as a non-query
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="Sql">A SQL Statement</param>

        public static void ExecStatement(string ServerName, string DatabaseName, string Sql)
        {
            using (SqlConnection Cnct = GetSqlConnection(ServerName, DatabaseName))
            {
                SqlCommand command = new SqlCommand(Sql, Cnct);
                command.CommandTimeout = Cfg.SQLTimeout;
                command.Connection.Open();
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes the passed statement as a query and returns the results
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="Sql">A SQL Statement</param>
        /// <returns>A DataSet with the results</returns>

        public static DataSet ExecSql(string ServerName, string DatabaseName, string Sql)
        {
            using (SqlConnection Cnct = GetSqlConnection(ServerName, DatabaseName))
            using (SqlDataAdapter adapter = new SqlDataAdapter())
            using (adapter.SelectCommand = new SqlCommand(Sql, Cnct))
            using (DataSet dataset = new DataSet())
            {
                adapter.SelectCommand.CommandTimeout = Cfg.SQLTimeout;
                adapter.Fill(dataset);
                return dataset;
            }
        }

        /// <summary>
        /// Builds a connection string and creates a new connection. The connection is not opened
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database. If null, the connection is just to the server, otherwise the database
        /// is specified in the initial catalog parameter of the connection string</param>
        /// <returns>The connection</returns>

        private static SqlConnection GetSqlConnection(string ServerName, string DatabaseName = null)
        {
            string CnctStr = string.Format("Data Source={0};Integrated Security=true;Connection Timeout={1}", ServerName, Cfg.SQLTimeout);
            if (DatabaseName != null)
            {
                CnctStr += string.Format(";Initial Catalog={0}", DatabaseName);
            }
            return new SqlConnection(CnctStr);
        }

        /// <summary>
        /// Determines if the passed table or view exists in the passed database
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="ObjectName">The table or view to determine the existence of</param>
        /// <returns>True if the table/view exists, else false</returns>

        public static bool TableOrViewExists(string ServerName, string DatabaseName, string ObjectName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = SchemaFromTableName(ObjectName);
                string TableNameWrk = ObjectFromObjName(ObjectName);
                return Db.Tables.Contains(TableNameWrk, SchemaName) ||
                    Db.Views.Contains(TableNameWrk, SchemaName);
            }
        }

        /// <summary>
        /// Drops the passed table
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableName">The table</param>

        public static void DropTable(string ServerName, string DatabaseName, string TableName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = SchemaFromTableName(TableName);
                string TableNameWrk = ObjectFromObjName(TableName);
                if (!Db.Tables.Contains(TableNameWrk, SchemaName))
                {
                    Log.InformationMessage("Settings indicated to drop table '{0}', but it does not exist. Bypassing this step. (Not an error.)", TableName);
                    return;
                }
                Log.InformationMessage("Dropping table: {0}", TableName);
                Db.Tables[TableNameWrk, SchemaName].DropIfExists();
            }
        }

        /// <summary>
        /// Truncates the passed table
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableOrViewName">The table or view</param>

        public static void TruncateTableOrView(string ServerName, string DatabaseName, string TableOrViewName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = SchemaFromTableName(TableOrViewName);
                string ToTruncate = ObjectFromObjName(TableOrViewName);
                if (Db.Tables.Contains(ToTruncate, SchemaName))
                {
                    Log.InformationMessage("Truncating table: {0}", TableOrViewName);
                    Db.Tables[ToTruncate, SchemaName].TruncateData();
                }
                else if (Db.Views.Contains(ToTruncate, SchemaName))
                {
                    throw new LoadException(string.Format("Could not truncate the passed view: {0}. View truncation is not currently supported", ToTruncate));
                    //Log.InformationMessage("Truncating view: {0}", TableOrViewName);
                    //View v = Db.Views[ToTruncate, SchemaName];
                    //TruncateTblUnderlyingView(v, Srvr, Db);
                }
                else
                {
                    Log.InformationMessage("Settings indicated to truncate object '{0}', but no table/view matching this name exists. Bypassing this step. (Not an error.)", TableOrViewName);
                    return;
                }
            }
        }

        // TODO fix this
        private static void TruncateTblUnderlyingView(View v, Server Srvr, Database Db)
        {
            DependencyWalker Dw = new DependencyWalker(Srvr);
            UrnCollection list = new UrnCollection();
            list.Add(v.Urn);
            List<string> Urns = new List<string>();
            DependencyTreeNode node = Dw.DiscoverDependencies(list, true);
            DependencyTreeNode child;
            if (node.HasChildNodes)
            {
                child = node.FirstChild;
                while (null != child)
                {
                    Urns.Add(child.Urn.XPathExpression[2].ToString());
                    child = child.NextSibling;
                }
            }
            if (Urns.Count != 1)
            {
                throw new LoadException(string.Format("Could not truncate the passed view because it does not contain exactly one underlying table: {0}", v.Name));
            }
            string SchemaName = SchemaFromTableName(Urns[0]);
            string ToTruncate = ObjectFromObjName(Urns[0]);
            if (Db.Tables.Contains(ToTruncate, SchemaName))
            {
                Log.InformationMessage("Truncating table: {0} underlying view: {1}", Urns[0], v.Name);
                Db.Tables[ToTruncate, SchemaName].TruncateData();
            }
        }


        /// <summary>
        /// Determines if the passed database name is valid
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <returns>True if the database exists on the server</returns>

        public static bool IsValidDatabaseName(string ServerName, string DatabaseName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                return Srvr.Databases.Contains(DatabaseName);
            }
        }

        /// <summary>
        /// Tests whether it is possible to connect to the passed server
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <returns>True if a connection can be established, else false</returns>

        public static bool CanConnect(string ServerName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName))
            {
                try
                {
                    SqlCnct.Open();
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Determines whether the passed schema is valid in the passed database
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="SchemaName">The schema name</param>
        /// <returns>True if the schema is valid, else false</returns>

        public static bool IsValidSchemaName(string ServerName, string DatabaseName, string SchemaName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                return Db.Schemas.Contains(SchemaName);
            }
        }

        /// <summary>
        /// Parses the table name - if it contains a schema prefix then returns it, else returns "dbo"
        /// </summary>
        /// <param name="TableName">Table name to parse</param>
        /// <returns>The schema from the passed table name, or "dbo" if the tables does not contain a schema specifier</returns>

        public static string SchemaFromTableName(string TableName)
        {
            string SchemaName = "dbo";
            if (TableName.Contains("."))
            {
                string[] tmp = TableName.Split('.');
                SchemaName = tmp[0];
                TableName = tmp[1];
            }
            return SchemaName;
        }

        /// <summary>
        /// Parses the table name and returns it. E.g. TableFromTableName("foo.bar") produces "foo"
        /// </summary>
        /// <param name="TableName">Table name to parse</param>
        /// <returns>The table portion it the passed table name contains a schema specifer, else simply returns the table name</returns>

        public static string ObjectFromObjName(string TableName)
        {
            if (TableName.Contains("."))
            {
                string[] tmp = TableName.Split('.');
                TableName = tmp[1];
            }
            return TableName;
        }

        /// <summary>
        /// Generates a bulk load completion message
        /// </summary>
        /// <param name="ServerName">The server that was loaded</param>
        /// <param name="DatabaseName">The database that was loaded</param>
        /// <param name="TableName">The table that was loaded</param>

        public static void GenerateCompletionMessage(string ServerName, string DatabaseName, string TableName)
        {
            string Sql = string.Format(
               @"select
                    sum(rows)
                from
                    sys.partitions
                where
                    index_id in (0, 1) and
                    object_id = object_id('{0}')
                group by
                    object_id",
               TableName);
            DataSet Ds = ExecSql(ServerName, DatabaseName, Sql);
            try
            {
                int RowCnt = int.Parse(Ds.Tables[0].Rows[0][0].ToString());
                Log.InformationMessage("{0} rows exist in the target table.\nTo view the data, use: select top 100 * from {1}.{2}",
                    RowCnt, DatabaseName.ToLower(), TableName.ToLower());
            } catch {
                Log.InformationMessage("Unable to obtain a record count from the target table. The table may not have loaded.");
            }
        }
    }
}
