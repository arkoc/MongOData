using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mongo.Context.Helpers
{
    public static class IMongoDatabaseExtensions
    {
        public static IQueryable GetCollectionAsQueryable(this IMongoDatabase database, string name, Type type)
        {
            var properties = type.GetProperties();
            var fields = type.GetFields();
            var genericGetCollectionMethod = typeof(IMongoDatabase).GetMethods()
                .Where(mi => mi.Name == "GetCollection")
                .Single();
            var getCollectionMethod = genericGetCollectionMethod.MakeGenericMethod(type);
            var mongoCollection = getCollectionMethod.Invoke(database, new object[] { name, null });

            var genericAsQueryableMethod = typeof(IMongoCollectionExtensions).GetMethods()
                .Where(x => x.Name == "AsQueryable")
                .Single();
            var asQueryableMethod = genericAsQueryableMethod.MakeGenericMethod(type);

            var queryableCollection = asQueryableMethod.Invoke(null, new object[] { mongoCollection, null }) as IQueryable;

            return queryableCollection;
        }
    }
}
