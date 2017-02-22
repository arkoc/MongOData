using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace Mongo.Context
{
    public partial class MongoContext : IDisposable
    {
        protected string connectionString;
        protected IMongoClient client;
        protected IMongoDatabase database;

        public MongoContext(string connectionString)
        {
            this.connectionString = connectionString;
            string databaseName = GetDatabaseName(this.connectionString);
            this.client = new MongoClient(this.connectionString);
            this.database = client.GetDatabase(databaseName);
        }

        public IMongoDatabase Database
        {
            get { return this.database; }
        }

        public static IEnumerable<string> GetDatabaseNames(string connectionString)
        {
            var databaseNames = new List<string>(0);
            using (var cursor = new MongoClient(connectionString).ListDatabasesAsync().Result)
            {
               cursor.ForEachAsync(dbDocument => databaseNames.Add(dbDocument["name"].AsString)).Wait();
            }
            return databaseNames;
        }

        public void Dispose()
        {
            
        }

        public void SaveChanges()
        {
        }

        private string GetDatabaseName(string connectionString)
        {
            var hostIndex = connectionString.IndexOf("//");
            if (hostIndex > 0)
            {
                int startIndex = connectionString.IndexOf("/", hostIndex + 2) + 1;
                int endIndex = connectionString.IndexOf("?", startIndex);
                if (startIndex > 0)
                {
                    if (endIndex > 0)
                        return connectionString.Substring(startIndex, endIndex - startIndex);
                    else
                        return connectionString.Substring(startIndex);
                }
            }

            throw new ArgumentException("Unsupported MongoDB connection string", "connectionString");
        }
    }
}
