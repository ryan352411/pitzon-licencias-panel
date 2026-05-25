using Npgsql;
using System;

namespace RefaccionariaPOS.Data
{
    public class DatabaseConnection
    {
        // ATENCIÓN: Cambia "tu_contraseña" por la contraseña real que le pusiste a PostgreSQL
        private readonly string connectionString = "Host=localhost;Username=postgres;Password=061106;Database=refaccionaria_db";

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(connectionString);
        }
    }
}