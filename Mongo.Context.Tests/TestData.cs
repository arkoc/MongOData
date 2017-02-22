using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Mongo.Context.Tests
{
    public static class TestData
    {
        public static void PopulateWithCategoriesAndProducts(bool clearDatabase = true)
        {
            var database = GetDatabase(clearDatabase);

            var categories = database.GetCollection<ClientCategory>("Categories");
            var products = database.GetCollection<ClientProduct>("Products");

            var categoryFood = new ClientCategory
                                   {
                                       Name = "Food",
                                       Products = null,
                                   };
            var categoryBeverages = new ClientCategory
                                        {
                                            Name = "Beverages",
                                            Products = null,
                                        };
            var categoryElectronics = new ClientCategory
                                          {
                                              Name = "Electronics",
                                              Products = null,
                                          };

            categories.InsertOne(categoryFood);
            categories.InsertOne(categoryBeverages);
            categories.InsertOne(categoryElectronics);

            products.InsertOne(
                new ClientProduct
                    {
                        ID = 1,
                        Name = "Bread",
                        Description = "Whole grain bread",
                        ReleaseDate = new DateTime(1992, 1, 1),
                        DiscontinueDate = null,
                        Rating = 4,
                        Quantity = new Quantity
                            {
                                Value = (double)12, 
                                Units = "pieces",
                            },
                        Supplier = new Supplier
                            {
                                Name = "City Bakery",
                                Addresses = new[]
                                    {
                                        new Address { Type = AddressType.Postal, Lines = new[] {"P.O.Box 89", "123456 City"} },
                                        new Address { Type = AddressType.Street, Lines = new[] {"Long Street 100", "654321 City"} },
                                    },
                            },
                        Category = categoryFood,
                    });
            products.InsertOne(
                new ClientProduct
                    {
                        ID = 2,
                        Name = "Milk",
                        Description = "Low fat milk",
                        ReleaseDate = new DateTime(1995, 10, 21),
                        DiscontinueDate = null,
                        Rating = 3,
                        Quantity = new Quantity
                            {
                                Value = (double)4,
                                Units = "liters",
                            },
                        Supplier = new Supplier
                            {
                                Name = "Green Farm",
                                Addresses = new[]
                                    {
                                        new Address { Type = AddressType.Street, Lines = new[] {"P.O.Box 123", "321321 Green Village"} },
                                    },
                            },
                        Category = categoryBeverages,
                    });
            products.InsertOne(
                new ClientProduct
                    {
                        ID = 3,
                        Name = "Wine",
                        Description = "Red wine, year 2003",
                        ReleaseDate = new DateTime(2003, 11, 24),
                        DiscontinueDate = new DateTime(2008, 3, 1),
                        Rating = 5,
                        Quantity = new Quantity
                            {
                                Value = (double)7,
                                Units = "bottles",
                            },
                        Category = categoryBeverages,
                    });
        }

        public static void PopulateWithClrTypes(bool clearDatabase = true)
        {
            var database = GetDatabase(clearDatabase);

            var clrTypes = database.GetCollection<ClrType>("ClrTypes");
            clrTypes.InsertOne(
                new ClrType
                {
                    BinaryValue = new[] { (byte)1 },
                    BoolValue = true,
                    NullableBoolValue = true,
                    DateTimeValue = new DateTime(2012, 1, 1),
                    NullableDateTimeValue = new DateTime(2012, 1, 1),
                    TimeSpanValue = new TimeSpan(1, 2, 3),
                    NullableTimeSpanValue = new TimeSpan(1, 2, 3),
                    GuidValue = Guid.Empty,
                    NullableGuidValue = Guid.Empty,
                    ByteValue = (byte)1,
                    NullableByteValue = (byte)1,
                    SByteValue = (sbyte)2,
                    NullableSByteValue = (sbyte)2,
                    Int16Value = 3,
                    NullableInt16Value = 3,
                    UInt16Value = 4,
                    NullableUInt16Value = 4,
                    Int32Value = 5,
                    NullableInt32Value = 5,
                    UInt32Value = 6,
                    NullableUInt32Value = 6,
                    Int64Value = 7,
                    NullableInt64Value = 7,
                    UInt64Value = 8,
                    NullableUInt64Value = 8,
                    SingleValue = 9,
                    NullableSingleValue = 9,
                    DoubleValue = 10,
                    NullableDoubleValue = 10,
                    DecimalValue = 11,
                    NullableDecimalValue = 11,
                    StringValue = "abc",
                    ObjectIdValue = new BsonObjectId(new ObjectId(100, 200, 300, 400)),
                });
        }

        public static void PopulateWithVariableTypes(bool clearDatabase = true)
        {
            var database = GetDatabase(clearDatabase);

            var variableTypes = database.GetCollection<BsonDocument>("VariableTypes");
            variableTypes.InsertOne(new TypeWithOneField { StringValue = "1" }.ToBsonDocument());
            variableTypes.InsertOne(new TypeWithTwoFields { StringValue = "2", IntValue = 2 }.ToBsonDocument());
            variableTypes.InsertOne(new TypeWithThreeFields { StringValue = "3", IntValue = 3, DecimalValue = 3m }.ToBsonDocument());
        }

        public static void PopulateWithBsonIdTypes(bool clearDatabase = true)
        {
            var database = GetDatabase(clearDatabase);

            var typesWithoutExplicitId = database.GetCollection<TypeWithoutExplicitId>("TypeWithoutExplicitId");
            typesWithoutExplicitId.InsertOne(new TypeWithoutExplicitId { Name = "A" });
            typesWithoutExplicitId.InsertOne(new TypeWithoutExplicitId { Name = "B" });
            typesWithoutExplicitId.InsertOne(new TypeWithoutExplicitId { Name = "C" });

            var typeWithBsonId = database.GetCollection<TypeWithBsonId>("TypeWithBsonId");
            typeWithBsonId.InsertOne(new TypeWithBsonId { Id = ObjectId.GenerateNewId(), Name = "A" });
            typeWithBsonId.InsertOne(new TypeWithBsonId { Id = ObjectId.GenerateNewId(), Name = "B" });
            typeWithBsonId.InsertOne(new TypeWithBsonId { Id = ObjectId.GenerateNewId(), Name = "C" });

            var typeWithIntId = database.GetCollection<TypeWithIntId>("TypeWithIntId");
            typeWithIntId.InsertOne(new TypeWithIntId { Id = 1, Name = "A" });
            typeWithIntId.InsertOne(new TypeWithIntId { Id = 2, Name = "B" });
            typeWithIntId.InsertOne(new TypeWithIntId { Id = 3, Name = "C" });

            var typeWithStringId = database.GetCollection<TypeWithStringId>("TypeWithStringId");
            typeWithStringId.InsertOne(new TypeWithStringId { Id = "1", Name = "A" });
            typeWithStringId.InsertOne(new TypeWithStringId { Id = "2", Name = "B" });
            typeWithStringId.InsertOne(new TypeWithStringId { Id = "3", Name = "C" });

            var typeWithGuidId = database.GetCollection<TypeWithGuidId>("TypeWithGuidId");
            typeWithGuidId.InsertOne(new TypeWithGuidId { Id = Guid.NewGuid(), Name = "A" });
            typeWithGuidId.InsertOne(new TypeWithGuidId { Id = Guid.NewGuid(), Name = "B" });
            typeWithGuidId.InsertOne(new TypeWithGuidId { Id = Guid.NewGuid(), Name = "C" });
        }

        public static void PopulateWithJsonSamples(bool clearDatabase = true)
        {
            var database = GetDatabase(clearDatabase);

            var jsonSamples = new[]
                {
                    "Colors", 
                    "Facebook", 
                    "Flickr", 
                    "GoogleMaps", 
                    "iPhone", 
                    "Twitter", 
                    "YouTube", 
                    "Nested", 
                    "ArrayOfNested", 
                    "ArrayInArray", 
                    "EmptyArray", 
                    "NullArray",
                    "UnresolvedArray",
                    "UnresolvedProperty",
                    "EmptyProperty",
                };

            foreach (var collectionName in jsonSamples)
            {
                var jsonCollection = GetResourceAsString(collectionName + ".json").Split(new string[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var json in jsonCollection)
                {
                    var doc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(json);
                    var collection = database.GetCollection<BsonDocument>(collectionName);
                    collection.InsertOne(doc);
                }
            }
        }

        public static void Clean()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["MongoDB"].ConnectionString;
            var client = new MongoClient(connectionString);
            client.DropDatabase(GetDatabaseName(connectionString));
        }

        public static IMongoDatabase CreateDatabase()
        {
            return GetDatabase(true);
        }

        public static IMongoDatabase OpenDatabase()
        {
            return GetDatabase(false);
        }

        private static IMongoDatabase GetDatabase(bool clear)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["MongoDB"].ConnectionString;
            var databaseName = GetDatabaseName(connectionString);
            var client = new MongoClient(connectionString);
            if (clear)
                client.DropDatabase(databaseName);
            return client.GetDatabase(databaseName);
        }

        private static string GetDatabaseName(string connectionString)
        {
            string databaseName = connectionString.Substring(connectionString.LastIndexOf("/")+1);
            int optionsIndex = databaseName.IndexOf("?");
            if (optionsIndex > 0)
            {
                databaseName = databaseName.Substring(0, optionsIndex);
            }
            return databaseName;
        }

        private static string GetResourceAsString(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var completeResourceName = assembly.GetManifestResourceNames().Single(o => o.EndsWith("." + resourceName));
            using (Stream resourceStream = assembly.GetManifestResourceStream(completeResourceName))
            {
                TextReader reader = new StreamReader(resourceStream);
                string result = reader.ReadToEnd();
                return result;
            }
        }
    }
}
