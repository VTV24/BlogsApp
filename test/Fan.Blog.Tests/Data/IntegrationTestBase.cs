using Fan.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace Fan.Tests.Data
{
    public class IntegrationTestBase : IDisposable
    {
        protected readonly FanDbContext _db;
        protected readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });

        public IntegrationTestBase()
        {
            var connection = new SqliteConnection() { ConnectionString = "Data Source=:memory:" };
            connection.Open();

            var options = new DbContextOptionsBuilder<FanDbContext>()
                .UseSqlite(connection).Options;

            _db = new FanDbContext(options, loggerFactory);
            _db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }
    }
}
