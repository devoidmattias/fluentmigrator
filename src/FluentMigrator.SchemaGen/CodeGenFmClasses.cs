﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using FluentMigrator.SchemaGen.SchemaReaders;
using FluentMigrator.SchemaGen.SchemaWriters;

namespace FluentMigrator.SchemaGen
{
    /// <summary>
    /// Code generate Fluent Migrator C# classes.
    /// Entry point API if EXE is used as a DLL
    /// </summary>
    public class CodeGenFmClasses
    {
        private readonly IOptions options;

        public CodeGenFmClasses(IOptions options)
        {
            this.options = options;
        }

        public IEnumerable<string> GenClasses()
        {
            // Generate migration classes for the whole schema of a single database.
            if (options.Db != null)
            {
                using (IDbConnection cnn = new SqlConnection(options.Db))
                {
                    cnn.Open();

                    // Simulate an empty database in DB #1 so the full scheme of DB #2 is generated.
                    IDbSchemaReader reader1 = new EmptyDbSchemaReader();
                    IDbSchemaReader reader2 = new SqlServerSchemaReader(cnn, options);

                    IMigrationWriter migrationWriter = new FmDiffMigrationWriter(options, reader1, reader2);
                    return migrationWriter.WriteMigrationClasses();
                }
            }
                // Generate migration classes based on differences between two databases.
            else if (options.Db1 != null && options.Db2 != null)
            {
                using (IDbConnection cnn1 = new SqlConnection(options.Db1))
                using (IDbConnection cnn2 = new SqlConnection(options.Db2))
                {
                    cnn1.Open();
                    cnn2.Open();

                    IDbSchemaReader reader1 = new SqlServerSchemaReader(cnn1, options);
                    IDbSchemaReader reader2 = new SqlServerSchemaReader(cnn2, options);

                    IMigrationWriter writer1 = new FmDiffMigrationWriter(options, reader1, reader2);

                    return writer1.WriteMigrationClasses();
                }
            }
            else
            {
                throw new Exception("Specificy EITHER --db OR --db1 and --db2 options.");
            }
        }

    }
}