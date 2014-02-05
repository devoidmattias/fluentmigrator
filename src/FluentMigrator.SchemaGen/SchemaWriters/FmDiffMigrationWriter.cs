﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using FluentMigrator.Model;
using FluentMigrator.SchemaGen.Extensions;
using FluentMigrator.SchemaGen.SchemaReaders;

namespace FluentMigrator.SchemaGen.SchemaWriters
{
    /// <summary>
    /// Writes a Fluent Migrator class for a database schema
    /// </summary>
    public class FmDiffMigrationWriter : IMigrationWriter
    {
        private readonly IOptions options;
        private readonly IDbSchemaReader db1;
        private readonly IDbSchemaReader db2;
        private int order = 0;

        private static int indent = 0;
        private static StreamWriter writer;
        private static StringBuilder sb;
        private static readonly Stack<StringBuilder> nestedBuffers = new Stack<StringBuilder>();

        public FmDiffMigrationWriter(IOptions options, IDbSchemaReader db1, IDbSchemaReader db2)
        {
            indent = 0;
            writer = null;
            sb = null;

            this.options = options;
            this.db1 = db1;
            this.db2 = db2;
            //this.tables1 = ApplyTableFilter(tables1);
            //this.tables2 = ApplyTableFilter(tables2);
        }

        #region Helpers

        private static void Indent()
        {
            if (sb == null)
            {
                for (int i = 0; i < indent; i++)
                {
                    writer.Write("    ");
                }
            }
            else
            {
                for (int i = 0; i < indent; i++)
                {
                    sb.Append("    ");
                }
            }
        }

        private void WriteLine()
        {
            if (sb == null)
            {
                writer.WriteLine();
            }
            else
            {
                sb.AppendLine();
            }
        }

        private static void WriteLine(string line)
        {
            Indent();
            if (sb == null)
            {
                writer.WriteLine(line);
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        private static void WriteLine(string format, params object[] args)
        {
            Indent();
            if (sb == null)
            {
                writer.WriteLine(format, args);
            }
            else
            {
                sb.AppendFormat(format, args);
                sb.AppendLine();
            }
        }

        private void WriteLines(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                WriteLine(line);
            }
        }

        private void WriteLines(IEnumerable<string> lines, string appendLastLine = null)
        {
            var lineArr = lines.ToArray();
            for (int i = 0; i < lineArr.Length; i++)
            {
                if (i < lineArr.Length - 1)
                {
                    WriteLine(lineArr[i]);
                }
                else
                {
                    WriteLine(lineArr[i] + appendLastLine);
                }
            }
        }

        private void BeginBlock()
        {
            WriteLine("{");
            indent++;
        }

        private void EndBlock()
        {
            indent--;
            WriteLine("}");
        }
        
        private void BeginBuffer()
        {
            if (sb != null) nestedBuffers.Push(sb);
            sb = new StringBuilder();
        }

        private string EndBuffer()
        {
            string result = sb.ToString();
            sb = nestedBuffers.Count == 0 ? null : nestedBuffers.Pop();
            return result;
        }

        protected class Indenter : IDisposable
        {
            protected internal Indenter()
            {
                indent++;
            }

            public void Dispose()
            {
                indent--;
            }
        }

        protected class Block : IDisposable
        {
            protected internal Block()
            {
                WriteLine("{");
                indent++;
            }

            public void Dispose()
            {
                indent--;
                WriteLine("}");
            }
        }

        protected void WriteClass(string dirName, string className, Action upMethod, Action downMethod = null)
        {
            // Prefix class with zero filled order number.
            className = string.Format("M{0,4:D4}0_{1}", ++order, className);

            string fullDirName = Path.Combine(options.BaseDirectory, dirName);
            new DirectoryInfo(fullDirName).Create();

            string classPath = Path.Combine(fullDirName, className + ".cs");
            Console.WriteLine(classPath);

            try
            {
                using (var fs = new FileStream(classPath, FileMode.Create))
                using (var writer1 = new StreamWriter(fs))   
                {
                    writer = writer1; // assigns class 'writer' variable

                    WriteLine("using System;");
                    WriteLine("using System.Collections.Generic;");
                    WriteLine("using System.Linq;");
                    WriteLine("using System.Web;");
                    WriteLine("using FluentMigrator;");
                    WriteLine("using AMPRO.Migrations.FM_Extensions;");

                    WriteLine(String.Empty);
                    WriteLine("namespace {0}.{1}", options.NameSpace, dirName.Replace("\\", "."));

                    using (new Block()) // namespace {}
                    {
                        WriteLine("[MigrationVersion({0})]", options.MigrationVersion.Replace(".", ", ") + ", " + order);
                        WriteLine("public class {0} : {1}", className, downMethod == null ? "AutoReversingMigrationExt" : "MigrationExt");
                        using (new Block()) // class {}
                        {
                            WriteMethod("Up", upMethod);

                            if (downMethod != null)
                            {
                                WriteMethod("Down", downMethod);
                            }
                        }
                    }

                    writer.Flush();
                    writer = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(classPath + ": Failed to write class file");
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null) Console.WriteLine(ex.InnerException.Message);
            }
        }

        private void WriteMethod(string name, Action body)
        {
            WriteLine();
            WriteLine("public override void {0}()", name);
            using (new Block())
            {
                body();
            }
        }

        private void CantUndo()
        {
            WriteLine("throw new Exception(\"Cannot undo this database upgrade\");");
        }

        #endregion

        public void WriteMigrations()
        {
            // Drop tables in order of their FK dependency.
            WriteClass("Common", "DropTables", DropTables, CantUndo);

            WriteClass("Common", "DropCode", DropCode, CantUndo);

            CreateUpdateTables();

            // Per table
            // Add new columns
            //  Placeholder to migrate data
            // Remove old columns
            // Drop old indexes - Do this for each table

            // Drop old SPs/Views/Functions

            // Drop old DataTypes

            // Create new DataTypes

            // Create new Tables

            // Drop old tables (OR may need to be renamed for manual rollback or data migration)

            // Alter tables 
            // Remove columns  (some will be renamed but that's up to the code reviewer)
            // Add columns               

            // Drop/Create new/modified Indexes

            // Drop/create new/modified FKs

            // Drop/Create new/modified SPs/Views/Functions 

            // Load Seed Data

            // Load Demo Data (if tagged "Demo")

            //foreach (TableDefinition table in newTables)
            //{
            //    CreateTable(table);
            //}

            //// Create foreign keys AFTER the tables.
            //foreach (TableDefinition table in tables)
            //{
            //    if (table.ForeignKeys.Any())
            //    {
            //        output.Write("{0}#region UP Table Foreign Keys {1}.{2}", Indent0, table.SchemaName, table.Name);
            //        foreach (var fk in table.ForeignKeys)
            //        {
            //            CreateForeignKey(fk);
            //        }
            //        output.WriteLine("{0}#endregion", Indent0);
            //    }
            //}

            //output.WriteLine("\t\t}\n"); //end method

        }

        #region Drop Tables and Code
        private void DropTables()
        {
            // TODO: Currently ignoring Schema name for table objects.
            var db1FkOrder = db1.TableFkDependencyOrder(false); // descending order

            var removedTableNames = db1.TableNames.Except(db2.TableNames).ToList();
            removedTableNames = removedTableNames.OrderBy(t => -db1FkOrder[t]).ToList();

            foreach (TableDefinition table in db1.GetTables(removedTableNames))
            {
                foreach (ForeignKeyDefinition fk in table.ForeignKeys)
                {
                    WriteLine("Delete.ForeignKey(\"{0}\").OnTable(\"{1}\").InSchema(\"{2}\");", fk.Name, fk.PrimaryTable, fk.PrimaryTableSchema);
                }

                WriteLine("Delete.Table(\"{0}\").InSchema(\"{1}\");", table.Name, table.SchemaName);
            }
        }

        private void DropCode()
        {
            foreach (var name in db1.StoredProcedures.Except(db2.StoredProcedures))
            {
                WriteLine("DeleteStoredProcedure(\"{0}\");", name);
            }

            foreach (var name in db1.Views.Except(db2.Views))
            {
                WriteLine("DeleteView(\"{0}\");", name);
            }

            foreach (var name in db1.UserDefinedFunctions.Except(db2.UserDefinedFunctions))
            {
                WriteLine("DeleteFunction(\"{0}\");", name);
            }

            foreach (var name in db1.UserDefinedDataTypes.Except(db2.UserDefinedDataTypes))
            {
                WriteLine("DeleteType(\"{0}\");", name);
            }

        }
        #endregion

        #region Create / Update Tables
        private void CreateUpdateTables()
        {
            var db1Tables = db1.Tables.ToDictionary(tbl => tbl.Name);

            // TODO: Currently ignoring Schema name for table objects.

            var db2FkOrder = db2.TableFkDependencyOrder(true);
            var db2TablesInFkOrder = db2.Tables.OrderBy(tableDef => db2FkOrder[tableDef.Name]);

            foreach (TableDefinition table in db2TablesInFkOrder)
            {
                TableDefinition newTable = table;

                if (db1Tables.ContainsKey(newTable.Name))
                {
                    TableDefinition oldTable = db1Tables[newTable.Name];

                    // Only generate a Table update class if there are detected changes.
                    BeginBuffer();
                    UpdateTable(oldTable, newTable);
                    string updates = EndBuffer();

                    if (updates.Length > 0)
                    {
                        string[] lines = updates.Replace(Environment.NewLine, "\n").Split('\n');
                        WriteClass("Common", "Update_" + table.Name, () => WriteLines(lines));
                    }
                }
                else
                {
                    WriteClass("Common", "Create_" + table.Name, () => CreateTable(newTable));
                }
            }

        }

        private void CreateTable(TableDefinition table)
        {
            //ColumnDefinition pkCol = table.Columns.FirstOrDefault(col => col.IsPrimaryKey);
            //bool hasClusteredPkIndex = table.Indexes.Any(index => index.IsClustered && index.IsUnique && index.Columns.All(col => col.Name == pkCol.Name));

            WriteLine("Create.Table(\"{1}\").InSchema(\"{0}\")", table.SchemaName, table.Name);
            
            using (new Indenter())
            {
                foreach (ColumnDefinition column in table.Columns)
                {
                    string colCode = GetColumnCode(column);
                    if (table.Columns.Last() == column) colCode += ";";

                    WriteLine(colCode);
                }
            }

            WriteLine();

            var nonColIndexes = GetNonColumnIndexes(table);

            WriteLines(nonColIndexes.Select(GetCreateIndexCode));
            WriteLines(table.ForeignKeys.Select(GetCreateForeignKeyCode));
        }

        /// <summary>
        /// Gets the set of tables indexes that are not declared as part of a table column definition
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        private IEnumerable<IndexDefinition> GetNonColumnIndexes(TableDefinition table)
        {
            // Names of indexes declared as as part of table column definition
            var colIndexNames = from col in table.Columns
                                where col.IsPrimaryKey || col.IsUnique || col.IsIndexed
                                select col.IsPrimaryKey ? col.PrimaryKeyName : col.IndexName;

            // Remaining indexes undeclared
            return from index in table.Indexes where !colIndexNames.Contains(index.Name) select index;
        }


        /// <summary>
        /// Generate code based on changes to table columns, indexes and foreign keys
        /// </summary>
        /// <param name="oldTable"></param>
        /// <param name="newTable"></param>
        private void UpdateTable(TableDefinition oldTable, TableDefinition newTable)
        {
            // Strategy is to generate FluentMigration API code for each named column, index and foreign key and then detect changes to names OR generated code.

            // Columns
            IDictionary<string, string> oldCols = oldTable.Columns.ToDictionary(col => col.Name, GetColumnCode);
            IDictionary<string, string> newCol = newTable.Columns.ToDictionary(col => col.Name, GetColumnCode);

            var addedColsCode = oldCols.GetAdded(newCol).Select(colCode => colCode.Replace("WithColumn", "AddColumn"));
            var updatedColsCode = oldCols.GetUpdated(newCol).Select(colCode => colCode.Replace("WithColumn", "AlterColumn"));
            var removedColsCode = oldCols.GetRemovedKeys(newCol).Select(colName => GetRemoveColumnCode(newTable, colName) );

            // Indexes
            IDictionary<string, string> oldIndexes = GetNonColumnIndexes(oldTable).ToDictionary(index => index.Name, GetCreateIndexCode);
            IDictionary<string, string> newIndexes = GetNonColumnIndexes(newTable).ToDictionary(index => index.Name, GetCreateIndexCode);
            
            // Updated indexes are removed and added
            var addedIndexCode = oldIndexes.GetAdded(newIndexes).Concat(oldIndexes.GetUpdated(newIndexes));
            var removedIndexNames = oldIndexes.GetRemovedKeys(newIndexes).Concat(oldIndexes.GetUpdatedKeys(newIndexes));
            var removedIndexCode = removedIndexNames.Select(indexName => GetRemoveIndexCode(oldTable, indexName));

            // Foreign Keys
            IDictionary<string, string> oldFKs = oldTable.ForeignKeys.ToDictionary(fk => fk.Name, GetCreateForeignKeyCode);
            IDictionary<string, string> newFKs = newTable.ForeignKeys.ToDictionary(fk => fk.Name, GetCreateForeignKeyCode);
            
            // Updated foreign keys are removed and added
            var addedFkCode = oldFKs.GetAdded(newFKs).Concat(oldFKs.GetUpdated(newFKs));
            var removedFkNames = oldFKs.GetRemovedKeys(newFKs).Concat(oldFKs.GetUpdatedKeys(newFKs));
            var removedFkCode = removedFkNames.Select(fkName => GetRemoveFKCode(oldTable, fkName));

            // Only Write lines if there are actual changes so we can detect when there are NO changes.

            AlterTable(newTable, addedColsCode);    // Added new columns
            WriteChanges(addedIndexCode);           // Added Indexes
            WriteChanges(addedFkCode);              // Added foreign keys

            AlterTable(newTable, updatedColsCode);  // Updated columns

            WriteChanges(removedFkCode);            // Removed foreign keys
            WriteChanges(removedIndexCode);         // Removed Indexes
            WriteChanges(removedColsCode);          // Removed new columns
        }

        private void AlterTable(TableDefinition table, IEnumerable<string> codeChanges)
        {
            var changes = codeChanges.ToList();
            if (changes.Any())
            {
                WriteLine("Alter.Table(\"{0}\").InSchema(\"{1}\")", table.Name, table.SchemaName);
                using (new Indenter())
                {
                    WriteLines(changes, ";");
                }
            }
        }

        private void WriteChanges(IEnumerable<string> codeChanges)
        {
            var changes = codeChanges.ToList();
            if (changes.Any())
            {
                WriteLine();
                WriteLines(changes);
            }
        }

        #endregion

        #region Column

        private string GetRemoveColumnCode(TableDefinition table, string colName)
        {
            return string.Format("Delete.Column(\"{0}\").FromTable(\"{1}\").InSchema(\"{2}\");", colName, table.Name, table.SchemaName);
        }

        private string GetColumnCode(ColumnDefinition column)
        {
            var sb = new StringBuilder();

            sb.AppendFormat(".WithColumn(\"{0}\").{1}", column.Name, GetMigrationTypeFunctionForType(column));

            if (column.IsIdentity) 
            {
                sb.Append(".Identity()");
            }

            if (column.IsPrimaryKey)
            {
                sb.AppendFormat(".PrimaryKey(\"{0}\")", column.PrimaryKeyName);
            }
            else if (column.IsUnique)
            {
                sb.AppendFormat(".Unique(\"{0}\")", column.IndexName);
            }
            else if (column.IsIndexed)
            {
                sb.AppendFormat(".Indexed(\"{0}\")", column.IndexName);
            }

            if (column.IsNullable.HasValue)
            {
                sb.Append(column.IsNullable.Value ? ".Nullable()" : ".NotNullable()");
            }

            if (column.DefaultValue != null && !column.IsIdentity)
            {
                sb.AppendFormat(".WithDefaultValue({0})", GetColumnDefaultValue(column));
            }

            //if (lastColumn) sb.Append(";");
            return sb.ToString();
        }

        private string GetMigrationTypeSize(DbType? type, int size)
        {
            if (size == -1) return "int.MaxValue";

            if (type == DbType.Binary && size == DbTypeSizes.ImageCapacity) return "DbTypeSizes.ImageCapacity";              // IMAGE fields
            if (type == DbType.AnsiString && size == DbTypeSizes.AnsiTextCapacity) return "DbTypeSizes.AnsiTextCapacity";    // TEXT fields
            if (type == DbType.String && size == DbTypeSizes.UnicodeTextCapacity) return "DbTypeSizes.UnicodeTextCapacity";  // NTEXT fields

            return size.ToString();
        }

        public string GetMigrationTypeFunctionForType(ColumnDefinition col)
        {
            var precision = col.Precision;
            string sizeStr = GetMigrationTypeSize(col.Type, col.Size);
            string precisionStr = (precision == -1) ? "" : "," + precision.ToString();
            string sysType = "AsString(" + sizeStr + ")";

            switch (col.Type)
            {
                case DbType.AnsiString:
                    if (options.UseDeprecatedTypes && col.Size == DbTypeSizes.AnsiTextCapacity)
                    {
                        sysType = "AsCustom(\"TEXT\")";
                    }
                    else
                    {
                        sysType = string.Format("AsAnsiString({0})", sizeStr);
                    }
                    break;
                case DbType.AnsiStringFixedLength:
                    sysType = string.Format("AsFixedLengthAnsiString({0})", sizeStr);
                    break;
                case DbType.String:
                    if (options.UseDeprecatedTypes && col.Size == DbTypeSizes.UnicodeTextCapacity)
                    {
                        sysType = "AsCustom(\"NTEXT\")";
                    }
                    else
                    {
                        sysType = string.Format("AsString({0})", sizeStr);
                    }
                    break;
                case DbType.StringFixedLength:
                    sysType = string.Format("AsFixedLengthString({0})", sizeStr);
                    break;
                case DbType.Binary:
                    if (options.UseDeprecatedTypes && col.Size == DbTypeSizes.ImageCapacity)
                    {
                        sysType = "AsCustom(\"IMAGE\")";
                    }
                    else
                    {
                        sysType = string.Format("AsBinary({0})", sizeStr);
                    }
                    break;
                case DbType.Boolean:
                    sysType = "AsBoolean()";
                    break;
                case DbType.Byte:
                    sysType = "AsByte()";
                    break;
                case DbType.Currency:
                    sysType = "AsCurrency()";
                    break;
                case DbType.Date:
                    sysType = "AsDate()";
                    break;
                case DbType.DateTime:
                    sysType = "AsDateTime()";
                    break;
                case DbType.Decimal:
                    sysType = string.Format("AsDecimal({0})", sizeStr + precisionStr);
                    break;
                case DbType.Double:
                    sysType = "AsDouble()";
                    break;
                case DbType.Guid:
                    sysType = "AsGuid()";
                    break;
                case DbType.Int16:
                case DbType.UInt16:
                    sysType = "AsInt16()";
                    break;
                case DbType.Int32:
                case DbType.UInt32:
                    sysType = "AsInt32()";
                    break;
                case DbType.Int64:
                case DbType.UInt64:
                    sysType = "AsInt64()";
                    break;
                case DbType.Single:
                    sysType = "AsFloat()";
                    break;
                case null:
                    sysType = string.Format("AsCustom({0})", col.CustomType);
                    break;
                default:
                    break;
            }

            return sysType;
        }

        public string GetColumnDefaultValue(ColumnDefinition col)
        {
            string sysType = null;
            string defValue = col.DefaultValue.ToString();

            var guid = Guid.Empty;
            switch (col.Type)
            {
                case DbType.Boolean:
                case DbType.Byte:
                case DbType.Currency:
                case DbType.Decimal:
                case DbType.Double:
                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.Single:
                case DbType.UInt16:
                case DbType.UInt32:
                case DbType.UInt64:
                    sysType = defValue.Replace("'", "").Replace("\"", "").CleanBracket();
                    break;

                case DbType.Guid:
                    if (defValue.IsGuid(out guid))
                    {
                        if (guid == Guid.Empty)
                            sysType = "Guid.Empty";
                        else
                            sysType = string.Format("new System.Guid(\"{0}\")", guid);
                    }
                    break;

                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.Date:
                    if (defValue.ToLower() == "current_time"
                        || defValue.ToLower() == "current_date"
                        || defValue.ToLower() == "current_timestamp")
                    {
                        sysType = "SystemMethods.CurrentDateTime";
                    }
                    else
                    {
                        sysType = "\"" + defValue.CleanBracket() + "\"";
                    }
                    break;

                default:
                    sysType = string.Format("\"{0}\"", col.DefaultValue);
                    break;
            }

            return sysType;
        }
        #endregion

        #region Index 

        private string GetRemoveIndexCode(TableDefinition table, string indexName)
        {
            return string.Format("Delete.Index(\"{0}\").OnTable(\"{1}\");", indexName, table.Name);
        }

        private string GetCreateIndexCode(IndexDefinition index)
        {
            var sb = new StringBuilder();

            //Create.Index("ix_Name").OnTable("TestTable2").OnColumn("Name").Ascending().WithOptions().NonClustered();
            sb.AppendFormat("Create.Index(\"{0}\").OnTable(\"{1}\")", index.Name, index.TableName);

            if (index.IsUnique)
            {
                sb.AppendFormat(".WithOptions().Unique()");
            }

            if (index.IsClustered)
            {
                sb.AppendFormat(".WithOptions().Clustered()");
            }

            foreach (var col in index.Columns)
            {
                sb.AppendFormat(".OnColumn(\"{0}\")", col.Name);
                sb.AppendFormat(".{0}()", col.Direction.ToString());
            }

            sb.Append(";");

            return sb.ToString();
        }

        #endregion

        #region Foreign Key

        private string GetRemoveFKCode(TableDefinition table, string fkName)
        {
            return string.Format("Delete.ForeignKey(\"{0}\").OnTable(\"{1}\").InSchema(\"{2}\");", fkName, table.Name, table.SchemaName);
        }

        private string ToStringArray(IEnumerable<string> cols)
        {
            string strCols = String.Join(", ", cols.Select(col => '"' + col + '"').ToArray());
            return '{' + strCols + '}';
        }

        protected string GetCreateForeignKeyCode(ForeignKeyDefinition fk)
        {
            BeginBuffer();

            //Create.ForeignKey("fk_TestTable2_TestTableId_TestTable_Id")
            //    .FromTable("TestTable2").ForeignColumn("TestTableId")
            //    .ToTable("TestTable").PrimaryColumn("Id");

            WriteLine("Create.ForeignKey(\"{0}\")", fk.Name);
            using (new Indenter())
            {
                // From Table
                WriteLine(".FromTable(\"{0}\")", fk.ForeignTable);

                using (new Indenter())
                {
                    if (fk.ForeignColumns.Count == 1)
                    {
                        WriteLine(".ForeignColumn(\"{0}\")", fk.ForeignColumns.First());
                    }
                    else
                    {
                        WriteLine("ForeignColumns({0})", ToStringArray(fk.ForeignColumns));
                    }
                }

                // To Table
                WriteLine(".ToTable(\"{0}\")", fk.PrimaryTable);

                using (new Indenter())
                {
                    if (fk.PrimaryColumns.Count == 1)
                    {
                        WriteLine(".PrimaryColumn(\"{0}\")", fk.PrimaryColumns.First());
                    }
                    else
                    {
                        WriteLine(".PrimaryColumns({0})", ToStringArray(fk.PrimaryColumns));
                    }
                }

                if (fk.OnDelete != Rule.None && fk.OnDelete == fk.OnUpdate)
                {
                    WriteLine(".OnDeleteOrUpdate(System.Data.Rule.{0})", fk.OnDelete);
                }
                else
                {
                    if (fk.OnDelete != Rule.None)
                    {
                        WriteLine(".OnDelete(System.Data.Rule.{0})", fk.OnDelete);
                    }

                    if (fk.OnUpdate != Rule.None)
                    {
                        WriteLine(".OnUpdate(System.Data.Rule.{0})", fk.OnUpdate);
                    }
                }
            }

            string result = EndBuffer();
            return result.Substring(0, result.Length-Environment.NewLine.Length) + ";";
        }

        #endregion
    }
}