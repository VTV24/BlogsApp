using Fan.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace Fan.Tests.Data
{
    public class IntegrationTestBase : IDisposable
    {
        /// <summary>
        /// A <see cref="FanDbContext"/> built with Sqlite in-memory mode.
        /// </summary>
        protected readonly FanDbContext _db;
        protected readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

        /// <summary>
        /// Initializes DbContext with SQLite Database Provider in-memory mode with logging to
        /// console and ensure database created.
        /// </summary>
        public IntegrationTestBase()
        {
            var connection = new SqliteConnection() { ConnectionString = "Data Source=:memory:" };
            connection.Open();

            var options = new DbContextOptionsBuilder<FanDbContext>()
                //.UseLoggerFactory(loggerFactory) // turn on logging to see generated SQL
                .UseSqlite(connection).Options;

            _db = new FanDbContext(options, loggerFactory);
            _db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted(); // important, otherwise SeedTestData is not erased
            _db.Dispose();
        }
    }
}
