# file-load

A C# console  utility to load files into SQL Server with a minimum of hassle. But also contains some features that support more complex file loading scenarios. The simplest use case is as follows. Given a CSV named `test1.csv` in the current working directory with this content:

```
this,is,line,one,with,some,data
and,this,is,another,line,with,some content in it
and,another,line that we have here,this,is,number,three
```

Then this command loads it: `load-file -file test1.csv -db testdb -delimiter comma`

Then: `select * from testdb.dbo.test1` produces:

| col0 | col1    | col2                   | col3    | col4 | col5   | col6               |
| :--- | :------ | :--------------------- | :------ | :--- | :----- | :----------------- |
| this | is      | line                   | one     | with | some   | data               |
| and  | this    | is                     | another | line | with   | some content in it |
| and  | another | line that we have here | this    | is   | number | three              |

So what happened?

The file was identified using the `-file` option as `test1.csv`. Since no database table name was give, the base name of the file was used as the table name. And since no schema was provided, `dbo` was selected by the utility.

If the filename had contained invalid characters for a SQL Server table name, then the utility would have conformed the filename to a valid table name. The `-db` option was specified as `testdb` so the table was loaded into that database. No server name was provided, so `localhost` was used with integrated security. Finally, the `-delimiter` was specified as `comma`, since `tab` is the default.

Since no data typing was specified, the columns table was created with all `varchar` columns of the necessary width to support the data. (This required a profiling step which the utility performed automatically.) And since no column name information was provided, the columns were named from `col0` to `col6` in ordinal position. The output of the utility was:

```
Started
Begin pre-process
Generating column names from input file
Column names generated. Column count: 7
Profiling file: test1.csv
Rows read: 3 -- Error rows: 0 -- Bytes read: 140 -- Elapsed time (HH:MM:SS.Milli): 00:00:00.0005488
Finish pre-process
Settings indicated to drop table 'dbo.test1', but it does not exist. Bypassing this step. (Not an error.)
Creating table: dbo.test1
Determining data type compatibility of source data with target table
Target table dbo.test1 appears to be compatible with source data
Beginning load of table: dbo.test1 from source file: test1.csv
Started SqlBulkCopy
Completed SqlBulkCopy. Loaded 3 rows
Load completed -- Elapsed time (HH:MM:SS.Milli): 00:00:00.0048244
3 rows exist in the target table.
To view the data, use: select top 100 * from testdb.dbo.test1
Normal completion
```

That's the basic use case: bang a file into a table with minimal intervention. The other features are best described by just documenting the supported command line options and parameters. Note - this utility uses my command-line parser `appsettings` for option parsing and displaying  usage instruction. That is a DLL project also hosted in GitHub: https://github.com/aceeric/appsettings.

### Command line options and parameters

**-file filespec** 
Mandatory. The path name of the file to load. Only a single file at a time is supported. This can be an absolute or relative path spec.

**-server server** 
Required unless `-noload` is specified. Specifies the server to load the data into. If not specified, `localhost` is used. Only Windows Integrated login is supported at this time.

**-db database**
The database to load the data into. Required unless `-noload` is specified. Must be an existing database.

**-tbl schema.table**

Optional. The table to load. If a dotted schema prefix is provided, then it is used, otherwise the table is accessed in the `dbo` schema. If the table does not exist, it is created with the supplied name. If a table name is not specified, then the utility creates a table in the dbo schema of the supplied database - generating a unique table name from the source file name. If a target table name is not specified, or, a table name is specified for a non-existent table, then the utility will profile the data regardless of whether the `-profile` option is supplied - so that it can determine the applicable data types for the columns in the table.

**-profile**
Optional. Indicates that the source data will be profiled to determine data types for each column. If the target table exists, the utility will ensure that the table is capable of storing the incoming data. If the table is not capable, then the utility will emanate an error message and stop. If profiling is not specified, and the load occurs into an existing table, then the load may fail due to data type incompatibilities between the source data and the target table. However, profiling can take a substantial amount of time for a large file so - if there is high confidence in data compatibility then profiling can be dispensed with when loading an existing table. (If the utility creates the table - either because no table name was specified or because a non-existent table name was specified, then profiling will be performed no matter what.)

When the utility creates the table, `varchar` columns are created with some extra space because experience has shown that columns often increase in width over time. The `-typed` option affects the behavior of this option (see below.)

**-bcp**
Optional. Indicates that Microsoft BULK INSERT will be used to load the data. BULK INSERT cannot skip headers so - if the incoming data has a header and the `-bcp` option is specified, then the file must be prepped first using the `-prep` option or the load will fail. If this option is not provided, then `SqlBulkCopy` is used to load the data, which is significantly slower than BULK INSERT.

**-prep**
Optional. Indicates that the source data will be prepped for bulk loading. If the source file contains headers, footers, or is quoted, then it cannot be loaded via BULK INSERT. To use BULK INSERT, the file must be *prepped*, which consists of removing headers and footers, and un-quoting and tab-delimiting the fields in the file. The prepped file will be placed in the same directory as the source file, with a suffix attached to the filename base. E.g.: `some-file.csv` will get prepped into `some-file.prepped.csv`. The utility will ensure there are no file name collisions by postfixing a number where required (e.g. `some-file.prepped-42.csv`). Note - the prepped file is always written out as a tab-delimited file. The goal is to prep it for BCP. Any tabs in incoming data fields will be replaced by spaces so that the presence of tabs in the prepped file can definitively be interpreted as field separators.

**-prepdir directory**
Optional. Ignored if the `-prep` option is not supplied. Specifies a directory to hold prepped files. The supplied value must be the name of an existing directory that the user has rights to create files in. If not provided, then prepped files are created in the same directory as the source file.

**-prepretain** 
Optional. Ignored if the `-prep` option is not supplied. Indicates that prepped files should not be deleted when the utility finishes. (Original files are never modified or removed.) The default is to delete prepped files after they have been loaded into the database.

**-typed**
Optional. Indicates that if the target table is to be created by the utility, it will be created with data types that match the source file data. This means, for example, that if the profiler determines that a column in the source file is an integer, it will define the column in the database table as an integer. If the profiler determines that a column is numeric(9,3) then it will be created that way. If this option is not specified, then all table columns are created as VARCHAR with a size sufficient to hold the source data. Has no effect unless the utility creates the target table. Note - omitting this argument significantly increases the performance of the data profiling step but could slow down the bulk import (if loading all varchars.)

**-split filespec** 
Optional. This option handles a specific data case where an incoming file has a single column like "AZ:Arizona", meaning it is a compound value containing a code and a description separated by some delimiter (in this example, a colon).

The parameter is the path name of a file that contains the names of columns to be split into two columns: a code column and a description column. The named file must contain exactly one column name on each line. If a split file is provided, the default split string is the colon character `:`. If a column needs to be split on a different string, then the split string for that column can be specified in the split file by postfixing that column name with a comma, followed by a bare or quote-enclosed split string. E.g.: `naics,"~"` would split the `naics` field into `naics` and `naics_descr` on the `tilde` character. Note - the file being loaded must have a header - or - a column name file must be supplied (see below for column name files) for splitting to work. The second column created by the utility is named the same as the original column with `_descr` appended. If splitting is specified, then the `-prep` option is required. A split file looks like this:

```
vendorcountrycode
maj_agency_cat
mod_agency
maj_fund_agency_cat
contractingofficeagencyid
contractingofficeid
fundingrequestingagencyid
fundingrequestingofficeid
contractactiontype," "
```

Above you can see each line contains a column name to split. All columns would be split on the colon character, except `contractactiontype` which is split on the first space. The first column `vendorcountrycode` would be split into `vendorcountrycode` and `vendorcountrycode_descr` and so on.

**-splitstr str** 

Optional. The default string to split on. If not supplied, colon (`:`) is used. Column-specific overrides in the split file override this as shown above, and the default split string. Ignored unless `-split` is specified.

**-showddl**
Optional. Indicates that the utility should display to the console the DDL statement that it would use to create the target table. If omitted, no DDL is displayed.

**-truncate** 
Optional. Indicates that the target table - if it exists - is to be truncated before being loaded. If not specified, then data is appended to an existing table. Ignored if the table does not exist, or will be dropped first per the `-drop` option.

**-noload**
Optional. Basically a "dry run" option. Indicates that the target table is not to be loaded. In this scenario, the utility performs all the other specified functions - profile, prep, frequency-generation - as dictated by supplied options, but does not actually load the server table. It does not perform any server-related activity - operating solely on the local file system objects. All server-related options are ignored.

**-drop**
Optional. Indicates that the target table - if it exists - is to be dropped before being loaded. In this case the utility will profile the data because it needs to determine the data types for the columns. If not specified, and an existing table name is supplied, then the table is loaded. Ignored if the table does not exist.

**-fixed list|@list** 
Optional. Indicates that the source file to be loaded is fixed field-width file. The parameter value is a comma-separated list of field widths, or the name of a file containing field widths. If this option is specified, then the `-delimiter` option and the `-simpleparse` option are ignored. If the list specifier is in the form `nnn,nnn,nnn...` then it is interpreted as a comma-separated list of field widths. If the list specifier begins with the at sign (`@`) then the at sign is removed and the remainder of the list specifier is interpreted as a filename. The file is opened and the field widths are built from the file. The field widths file can contain multiple lines, with multiple width specifiers per line, separated by commas. If this option is omitted, then the input file is treated as a delimited file.

Here is an example of how you would load a fixed width file using this option. This is a DOS cmd file named `load-sam-fixed.cmd`:

```
@echo off
load-file ^
 -file "sam-fixed.txt" ^
 -tbl sam_monthly_from_fixed ^
 -fixed 11,20,20,20,20,23,17,15,16,15,255,255,255,20,255,255,255,255,20,18,20,26,19,26,500,20,22,24,21,255,13,18,7998,16,2500,20,20,255,255,255,255,30,23,255,255,27,255,255,255,255,255,50,27,25,255,21,25,50,24,255,255,31,255,255,255,255,255,50,31,29,255,25,29,50,28,255,255,32,255,255,255,255,255,50,28,26,50,22,26,50,25,255,255,32,255,255,255,255,255,50,32,30,50,26,30,50,29,255,255,27,255,255,255,255,255,50,27,25,255,21,25,50,24,255,255,35,255,255,255,255,255,50,31,29,50,25,29,50,28,255,23,255,28,21,26,50,22,25,255,23 ^
 ... other options omitted
```

You can see that the parameter value is just a long comma-separated list of column widths with no spaces. *(Note: It's usually not good when these width specifiers don't match the actual file format...)*

**-tabsize n**
Optional. Only used if the `-fixed` argument is provided. Causes tabs in the input file to be expanded to the specified number of spaces. If not provided, the default is for tabs to be expanded to four spaces.

**-delimiter delim**
Optional. Specifies the field delimiter for the input file. Allowed literals are `pipe`, `comma`, `tab`, and `auto`. E.g. `-delimiter tab`. If not supplied, then `tab` is used as the default. If `auto` is specified then the utility will attempt to determine the delimiter by scanning the first thousand records of the file and looking for a delimiter from the supported set that consistently splits the lines in the file into identical numbers of columns.

**-maxrows n**
Optional. Process at most `n` data rows from the file. (Does not include header or skipped rows.) The default is to process all input data rows. Good for a quick examination of the top of a large file.

**-preview n**
Optional. Displays the first `n` rows of the file to the console and then exits without performing any additional processing. Useful to visually validate that the input file is being properly parsed by the utility according to the supplied options.

**-simpleparse**
Optional. If supplied, the utility will not attempt to perform field parsing based on quotes in the incoming file. It will simply split each line on the specified delimiter. Useful in cases where the file to load *is definitely known* not to contain delimiters embedded within fields. If not provided then the utility performs quote parsing of the file data to handle embedded delimiters (which slows the process.)

**-skiplines n**
Optional. Do not process the first `n` lines of the input file. The utility will read those lines and discard them as if they were not present in the file. Applicable if the file has a header row or rows that you want to ignore, or any non-data rows at the top. The default is to process all input lines.

**-headerline n**
Optional. Defines the 1-relative line number in the file that contains a header. If provided, and the utility is directed to create the target table, then the utility will use the column names from the header row, after converting them to valid SQL identifiers. If this option is specified, then `-skiplines` can be omitted if the header row is the only non-data row at the head of the file. If both are specified, then both values are used independently, not additively or relatively. For example, if `-skiplines` = 1 and `-headerline` = 1, then the utility will read the header from line one (1-relative), and also ignore that line (it is skipped.) If `-skiplines` = 5 and `-headerline` = 1, then the utility will read the header from line one, ignore that line as non-data, and ignore the next four lines for a total of 5 skipped lines. The `-headerline` value cannot be greater than `-skiplines`. If the file has a header, but there is no desire to use it to generate column names, then skip over it with `-skiplines`, and omit the `-headerline` option.

**-eofstr eofstr**
Optional. Some files have an EOF line to terminate the data, but that is not the last line in the file. In this scenario, the EOF line always begins with a specific string value. If the file being processed has such an EOF line, then provide the first part of the EOF string here and the utility will stop processing when the string is encountered at the start of the first matching line. Note: the match is an exact match, including case. If the EOF string contains a space, enclose the string in quotes. (E.g. `-eofstr "EOF PUBLIC"`)

**-colfile filespec**
Optional. Applicable if the utility is going to create the target table. If supplied, the utility will read the column names from the specified file. The order of columns in the column names file should match that of the source file. The column name file must have one column name per row. If the column names file is supplied, the table will be created with those names. (This option overrides the `-headerline` option.) If omitted, and the source file does not have a header, and the utility is directed to create the target table, then the target table column names will be named `col0`, `col1`,` col2`... etc. NOTE: If a column file is specified, then it must **not** include additional column names for splitting. It must align with the original **unsplit** file. (Splitting will generate the new column names as described.)

Here is the first part of a column name that was used to load the *sam.gov* data, which didn't contain column names:

```
duns
dunsplus4
cage_code
dodaac
sam_extract_code
purpose_of_registration
registration_date
expiration_date
last_update_date
activation_date
legal_business_name
dba_name
```

**-maxerrors n**
Optional. Specifies the maximum number of error records that the utility will allow before aborting. An error record is defined as one having an incorrect column count. The default value is zero: i.e. the first error causes the process to abort. A value of -1 indicates that all errors are to be ignored. The column count for the file is determined by the header line, the column names file, or the first non-skipped line.

**-errfile filespec**
Optional. Indicates that the utility should write error records to the specified error file if the number of columns in any given row don't match the rest of the file. The *correct* number of columns is determined by the header, if one exists, or the column names file, if specified, otherwise the first non-skipped record in the file. (The utility doesn't handle ragged files at this time.)

**-freqfile filespec**
Optional. Ignored unless the `-profile` option is provided. Specifies the path of a file to write frequencies to. Each column in the input file will be freq'd in the freq file unless the number of distinct values for a column exceeds 1000, then only the first 1000 values will be freq'd for that column. Note: generating frequencies will increase the profiling time for large files. Here is what a frequency file looks like:

```
FLD: Contact Prefix (8)
=======================
Value	Count
=====	=====
        6416
dr      23
dr.     3
mr      15895
mr.     91
ms      3945
ms.     6
sir     1

FLD: Bonus (US Dollars) (6)
===========================
Value	Count
=====	=====
        26375
100000  1
292500  1
370000  1
600     1
765000  1

etc...
```

**-sqltimeout hrs**
Optional. Specifies the command timeout for executing SQL commands against the server. The value specified is in hours. Decimal values are allowed (e.g. .25 for 15 minutes). If this option is not provided, then one (1) hour is used as the default.

**-log file|db|con**
Optional. Determines how the utility communicates errors, status, etc. If not supplied, then all output goes to the console. If `file` is specified, then the utility logs to a log file in the same directory that the utility is run from. The log file will be named `load-file.log`. If `db` is specified, then logging occurs to the database. If `con` is specified, then output goes to the console (same as if the option were omitted.) If logging to file or db is specified then the utility runs silently with no console output.

If db logging is specified, then the required logging components must be installed in the database. If the components are not installed and db logging is specified, then the utility will automatically fail over to file-based logging.

Note: the C# utilizing this option requires the inclusion of my logging DLL which is also in GitHub: https://github.com/aceeric/logger. This utility also includes DDL for setting up database logging.

**-loglevel err|warn|info**
Optional. Defines the logging level. `err` specifies that only errors will be reported. `warn` means errors and warnings, and `info` means all messages. The default is `info`.

**-jobid guid**
Optional. Defines a job ID for the logging subsystem. A GUID value is supplied in the canonical 8-4-4-4-12 form. If provided, then the logging subsystem is initialized with the provided GUID. The default behavior is for the logging subsystem to generate its own GUID. The idea behind this option is - loading a file  might be one step in a multi-step job. Logging to the database and identifying each step with a GUID allows one to tie together job steps executed across different tooling using the job GUID.

### Some examples

Here are a couple of examples of using the utility. Each example is assumed to be the contents of a DOS cmd file:

**sam.gov**

This example loads a SAM file from sam.gov. It's a fixed width file. The column widths are specified. The data is loaded into the `testdb` database on the local SQL Server. No table name is provided so the table will be `sam-fixed`. Embedded tab characters in column values are replaced by four spaces. The data is profiled to determine the column widths and the data is prepped so it can be loaded with bulk copy (meaning it is written out tab-separated). Bulk copy is used to fast load the data. The target table is dropped first. The prep directory is the working directory. The target table will be typed with the most appropriate data types. The max errors before aborting the load are 10. Error records are written to an error file. The header line is line 1 (will be stripped in the prep file). INFO logging occurs to the console:

```
@echo off
load-file ^
 -file "sam-fixed.txt" ^
 -server localhost ^
 -db testdb ^
 -tbl sam_monthly_from_fixed ^
 -fixed 11,20,20,20,20,23,17,15,16,15,255,255,255,20,255,255,255,255,20,18,20,26,19,26,500,20,22,24,21,255,13,18,7998,16,2500,20,20,255,255,255,255,30,23,255,255,27,255,255,255,255,255,50,27,25,255,21,25,50,24,255,255,31,255,255,255,255,255,50,31,29,255,25,29,50,28,255,255,32,255,255,255,255,255,50,28,26,50,22,26,50,25,255,255,32,255,255,255,255,255,50,32,30,50,26,30,50,29,255,255,27,255,255,255,255,255,50,27,25,255,21,25,50,24,255,255,35,255,255,255,255,255,50,31,29,50,25,29,50,28,255,23,255,28,21,26,50,22,25,255,23 ^
 -tabsize 4 ^
 -profile ^
 -bcp ^
 -prep ^
 -drop ^
 -prepdir ./ ^
 -typed ^
 -maxerrors 10 ^
 -errfile "usaspending.error.txt" ^
 -headerline 1 ^
 -log con ^
 -loglevel info

```

**usaspending.gov**

This example profiles  a USA Spending file but does not load it. The incoming file is a TSV so the delimiter is specified as tab. The DDL that would be used to created the table is displayed to the console. A split file is specified to split a number of compound columns. (The split file is shown below.)  The default split string is `:`. (Which is the default but it's specified here anyway.) There is no limit to errors, and since there is no error file the errors are just ignored. INFO logging to the console as above.

```
@echo off
load-file ^
 -file "D:\DATA\USASpending\datafeeds\2018_All_Contracts_Full_20180115.tsv" ^
 -db ingest ^
 -tbl tbl_usaspending2018 ^
 -profile ^
 -noload ^
 -showddl ^
 -split ./splitcols.txt ^
 -splitstr : ^
 -typed ^
 -delimiter tab ^
 -maxerrors -1 ^
 -headerline 1 ^
 -log con ^
 -loglevel info
```

First few lines of the split file:

```
vendorcountrycode
maj_agency_cat
mod_agency
maj_fund_agency_cat
contractingofficeagencyid
contractingofficeid
fundingrequestingagencyid
fundingrequestingofficeid
contractactiontype," "
reasonformodification
typeofcontractpricing
subcontractplan
contingencyhumanitarianpeacekeepingoperation
```

