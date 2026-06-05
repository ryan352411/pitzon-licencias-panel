using Npgsql;
using System;

namespace RefaccionariaPOS.Data
{
    public class DatabaseConnection
    {
        private const string LocalConnectionString = "Host=localhost;Username=postgres;Password=061106;Database=refaccionaria_db";

        private readonly string connectionString =
            Environment.GetEnvironmentVariable("REFACCIONARIA_DB_CONNECTION")
            ?? LocalConnectionString;

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(connectionString);
        }
    }
}
