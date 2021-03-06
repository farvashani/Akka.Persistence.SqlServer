﻿//-----------------------------------------------------------------------
// <copyright file="DbUtils.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Xml;
using System.Data.SqlClient;
using System.IO;

namespace Akka.Persistence.SqlServer.Tests
{
    public static class DbUtils
    {
        public static IConfigurationRoot Config { get; private set; }

        public static string ConnectionString { get; private set; }

        public static void Initialize()
        {
            Config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddXmlFile("AppConfig.xml").Build();
            ConnectionString = Config.GetSection("connectionStrings:add:TestDb")["connectionString"];
            var connectionBuilder = new SqlConnectionStringBuilder(ConnectionString);

            //connect to postgres database to create a new database
            var databaseName = connectionBuilder.InitialCatalog;
            connectionBuilder.InitialCatalog = "master";
            ConnectionString = connectionBuilder.ToString();

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();

                using (var cmd = new SqlCommand())
                {
                    cmd.CommandText = string.Format(@"
                        IF db_id('{0}') IS NULL
                            BEGIN
                                CREATE DATABASE {0}
                            END
                            
                    ", databaseName);
                    cmd.Connection = conn;

                    var result = cmd.ExecuteScalar();
                }

                DropTables(conn, databaseName);

                // set this back to the journal/snapshot database
                connectionBuilder.InitialCatalog = databaseName;
                ConnectionString = connectionBuilder.ToString();
            }
        }

        public static void Clean()
        {
            var connectionBuilder = new SqlConnectionStringBuilder(ConnectionString);
            var databaseName = connectionBuilder.InitialCatalog;
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                DropTables(conn, databaseName);
            }
        }

        private static void DropTables(SqlConnection conn, string databaseName)
        {
            using (var cmd = new SqlCommand())
            {
                cmd.CommandText = $@"
                    USE {databaseName};
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'EventJournal') BEGIN DROP TABLE dbo.EventJournal END;
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Metadata') BEGIN DROP TABLE dbo.Metadata END;
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'SnapshotStore') BEGIN DROP TABLE dbo.SnapshotStore END;";
                cmd.Connection = conn;
                cmd.ExecuteNonQuery();
            }
        }
    }
}