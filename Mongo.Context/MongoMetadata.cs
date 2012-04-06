﻿using System;
using System.Collections.Generic;
using System.Data.Services.Providers;
using System.Linq;
using System.Text;
using DataServiceProvider;
using MongoDB.Bson;

namespace Mongo.Context
{
    public class MongoMetadata
    {
        public static readonly string MappedObjectIdName = "db_id";
        public static readonly Type MappedObjectIdType = typeof(string);
        public static readonly string ContainerName = "MongoContext";
        public static readonly string RootNamespace = "Mongo";
        public static readonly bool UseGlobalComplexTypeNames = true;

        public DSPMetadata CreateMetadata(string connectionString)
        {
            var metadata = new DSPMetadata(ContainerName, RootNamespace);

            using (MongoContext context = new MongoContext(connectionString))
            {
                PopulateMetadata(metadata, context);
            }

            return metadata;
        }

        protected IEnumerable<string> GetCollectionNames(MongoContext mongoContext)
        {
            return mongoContext.Database.GetCollectionNames().Where(x => !x.StartsWith("system."));
        }

        protected void PopulateMetadata(DSPMetadata metadata, MongoContext context)
        {
            foreach (var collectionName in GetCollectionNames(context))
            {
                var collection = context.Database.GetCollection(collectionName);
                var document = collection.FindOne();
                if (document != null)
                {
                    var resourceSet = metadata.ResourceSets.SingleOrDefault(x => x.Name == collectionName);
                    if (resourceSet == null)
                    {
                        RegisterResourceType(metadata, context, collectionName, document, ResourceTypeKind.EntityType);
                    }
                }
            }
        }

        private ResourceType RegisterResourceType(DSPMetadata metadata, MongoContext context, 
            string collectionName, BsonDocument document, ResourceTypeKind resourceTypeKind)
        {
            var collectionType = resourceTypeKind == ResourceTypeKind.EntityType
                                     ? metadata.AddEntityType(collectionName)
                                     : metadata.AddComplexType(collectionName);

            bool hasObjectId = false;
            foreach (var element in document.Elements)
            {
                var elementType = GetElementType(element);
                if (IsObjectId(element))
                {
                    if (resourceTypeKind == ResourceTypeKind.EntityType)
                        metadata.AddKeyProperty(collectionType, MappedObjectIdName, elementType);
                    else
                        metadata.AddPrimitiveProperty(collectionType, MappedObjectIdName, elementType);
                    hasObjectId = true;
                }
                else if (elementType == typeof(BsonDocument))
                {
                    ResourceType resourceType = null;
                    var resourceSet = metadata.ResourceSets.SingleOrDefault(x => x.Name == element.Name);
                    if (resourceSet != null)
                    {
                        resourceType = resourceSet.ResourceType;
                    }
                    else
                    {
                        resourceType = RegisterResourceType(metadata, context, 
                            GetComplexTypeName(collectionName, element.Name), 
                            element.Value.AsBsonDocument, ResourceTypeKind.ComplexType);
                    }
                    metadata.AddComplexProperty(collectionType, element.Name, resourceType);
                }
                else if (elementType == typeof(BsonArray))
                {
                    var bsonArray = element.Value.AsBsonArray;
                    if (bsonArray != null && bsonArray.Count > 0)
                    {
                        // TODO
                        //var referencedCollection = GetDocumentCollection(context, bsonArray[0].AsBsonDocument);
                        //if (referencedCollection != null)
                        //{
                        //    resourceReferences.Add(new Tuple<ResourceType, string, string>(collectionType, element.Name, referencedCollection.Name));
                        //}
                    }
                }
                else
                {
                    metadata.AddPrimitiveProperty(collectionType, element.Name, elementType);
                }
            }

            if (resourceTypeKind == ResourceTypeKind.EntityType)
            {
                if (!hasObjectId)
                    metadata.AddKeyProperty(collectionType, MappedObjectIdName, MappedObjectIdType);

                metadata.AddResourceSet(collectionName, collectionType);
            }

            return collectionType;
        }

        public static bool IsObjectId(BsonElement element)
        {
            return element.Value.RawValue != null && 
                (element.Value.RawValue.GetType() == typeof (ObjectId) || element.Value.RawValue.GetType() == typeof (BsonObjectId));
        }

        private static Type GetElementType(BsonElement element)
        {
            if (IsObjectId(element))
            {
                return MappedObjectIdType;
            }
            else if (element.Value.RawValue != null)
            {
                switch (element.Value.BsonType)
                {
                    case BsonType.DateTime:
                        return typeof(DateTime);
                    default:
                        return element.Value.RawValue.GetType();
                }
            }
            else if (element.Value.GetType() == typeof(BsonArray) || element.Value.GetType() == typeof(BsonDocument))
            {
                return element.Value.GetType();
            }
            else
            {
                switch (element.Value.BsonType)
                {
                    case BsonType.Binary:
                        return typeof(byte[]);
                    default:
                        return typeof(string);
                }
            }
        }

        internal static string GetComplexTypePrefix(string ownerName)
        {
            return UseGlobalComplexTypeNames ? string.Empty : ownerName;
        }

        internal static string GetComplexTypeName(string collectionName, string resourceName)
        {
            return UseGlobalComplexTypeNames ? resourceName : string.Join("__", collectionName, resourceName);
        }
    }
}
