﻿using System;
using System.Collections.Generic;
using System.Data.Services.Providers;
using System.Linq;
using System.Text;
using DataServiceProvider;
using MongoDB.Bson;

namespace Mongo.Context
{
    class MongoMetadataCache
    {
        public DSPMetadata DspMetadata { get; set; }
        public Dictionary<string, Type> ProviderTypes { get; set; }
        public Dictionary<string, Type> GeneratedTypes { get; set; }
    }

    class CollectionProperty
    {
        public ResourceType CollectionType { get; set; }
        public string PropertyName { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is CollectionProperty)
            {
                var prop = obj as CollectionProperty;
                return this.CollectionType == prop.CollectionType && this.PropertyName == prop.PropertyName;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return this.CollectionType.GetHashCode() ^ this.PropertyName.GetHashCode();
        }
    }

    public class MongoMetadata
    {
        public static readonly string ProviderObjectIdName = "_id";
        public static readonly string MappedObjectIdName = "db_id";
        public static readonly Type ProviderObjectIdType = typeof(BsonObjectId);
        public static readonly Type MappedObjectIdType = typeof(string);
        public static readonly string ContainerName = "MongoContext";
        public static readonly string RootNamespace = "Mongo";
        public static readonly bool UseGlobalComplexTypeNames = false;
        public static readonly bool CreateDynamicTypesForComplexTypes = true;
        internal static readonly string WordSeparator = "__";
        internal static readonly string PrefixForInvalidLeadingChar = "x";

        private string connectionString;
        private DSPMetadata dspMetadata;
        private readonly Dictionary<string, Type> providerTypes;
        private readonly Dictionary<string, Type> generatedTypes;
        private readonly List<CollectionProperty> unresolvedProperties = new List<CollectionProperty>();
        private static readonly Dictionary<string, MongoMetadataCache> MetadataCache = new Dictionary<string, MongoMetadataCache>();

        public MongoConfiguration.Metadata Configuration { get; private set; }
        internal Dictionary<string, Type> ProviderTypes { get { return this.providerTypes; } }
        internal Dictionary<string, Type> GeneratedTypes { get { return this.generatedTypes; } }

        public MongoMetadata(string connectionString, MongoConfiguration.Metadata metadata = null)
        {
            this.connectionString = connectionString;
            this.Configuration = metadata ?? MongoConfiguration.Metadata.Default;
            lock (MetadataCache)
            {
                MongoMetadataCache metadataCache;
                MetadataCache.TryGetValue(this.connectionString, out metadataCache);
                if (metadataCache == null)
                {
                    metadataCache = new MongoMetadataCache
                                        {
                                            DspMetadata = new DSPMetadata(ContainerName, RootNamespace),
                                            ProviderTypes = new Dictionary<string, Type>(),
                                            GeneratedTypes = new Dictionary<string, Type>(),
                                        };
                    MetadataCache.Add(this.connectionString, metadataCache);
                }
                this.dspMetadata = metadataCache.DspMetadata;
                this.providerTypes = metadataCache.ProviderTypes;
                this.generatedTypes = metadataCache.GeneratedTypes;
            }

            using (var context = new MongoContext(connectionString))
            {
                PopulateMetadata(context);
            }
        }

        public DSPMetadata CreateDSPMetadata()
        {
            return this.dspMetadata.Clone() as DSPMetadata;
        }

        public static void ResetDSPMetadata()
        {
            MetadataCache.Clear();
        }

        public ResourceType ResolveResourceType(string resourceName, string ownerPrefix = null)
        {
            ResourceType resourceType;
            string qualifiedResourceName = string.IsNullOrEmpty(ownerPrefix) ? resourceName : MongoMetadata.GetQualifiedTypeName(ownerPrefix, resourceName);
            this.dspMetadata.TryResolveResourceType(string.Join(".", MongoMetadata.RootNamespace, qualifiedResourceName), out resourceType);
            return resourceType;
        }

        public ResourceSet ResolveResourceSet(string resourceName)
        {
            return this.dspMetadata.ResourceSets.SingleOrDefault(x => x.Name == resourceName);
        }

        public ResourceProperty ResolveResourceProperty(ResourceType resourceType, BsonElement element)
        {
            var propertyName = MongoMetadata.GetResourcePropertyName(element, resourceType.ResourceTypeKind);
            return resourceType.Properties.SingleOrDefault(x => x.Name == propertyName);
        }

        private IEnumerable<string> GetCollectionNames(MongoContext context)
        {
            return context.Database.GetCollectionNames().Where(x => !x.StartsWith("system."));
        }

        private void PopulateMetadata(MongoContext context)
        {
            foreach (var collectionName in GetCollectionNames(context))
            {
                var collection = context.Database.GetCollection(collectionName);
                var resourceSet = ResolveResourceSet(collectionName);

                var documents = collection.FindAll();
                if (this.Configuration.PrefetchRows == 0)
                {
                    if (resourceSet == null)
                    {
                        RegisterResourceSet(context, collectionName);
                    }
                }
                else
                {
                    int rowCount = 0;
                    foreach (var document in documents)
                    {
                        if (resourceSet == null)
                        {
                            resourceSet = RegisterResourceSet(context, collectionName, document);
                        }
                        else
                        {
                            UpdateResourceSet(context, resourceSet, document);
                        }

                        ++rowCount;
                        if (this.Configuration.PrefetchRows >= 0 && rowCount >= this.Configuration.PrefetchRows)
                            break;
                    }
                }
            }

            foreach (var prop in this.unresolvedProperties)
            {
                var providerType = typeof(string);
                var propertyName = NormalizeResourcePropertyName(prop.PropertyName);
                this.dspMetadata.AddPrimitiveProperty(prop.CollectionType, propertyName, providerType);
                this.providerTypes.Add(string.Join(".", prop.CollectionType.Name, propertyName), providerType);
            }
        }

        private ResourceSet RegisterResourceSet(MongoContext context, string collectionName, BsonDocument document = null)
        {
            RegisterResourceType(context, collectionName, document, ResourceTypeKind.EntityType);
            return ResolveResourceSet(collectionName);
        }

        private void UpdateResourceSet(MongoContext context, ResourceSet resourceSet, BsonDocument document)
        {
            foreach (var element in document.Elements)
            {
                var propertyName = GetResourcePropertyName(element, ResourceTypeKind.EntityType);
                var resourceProperty = resourceSet.ResourceType.Properties.SingleOrDefault(x => x.Name == propertyName);
                if (resourceProperty == null)
                {
                    RegisterOrUpdateResourceProperty(context, resourceSet.ResourceType, element);
                }
                else if ((resourceProperty.Kind & ResourcePropertyKind.ComplexType) != 0 && element.Value != BsonNull.Value)
                {
                    UpdateComplexProperty(context, resourceSet.ResourceType, element);
                }
            }
        }

        private ResourceType RegisterResourceType(MongoContext context, string collectionName, BsonDocument document, ResourceTypeKind resourceTypeKind)
        {
            var collectionType = resourceTypeKind == ResourceTypeKind.EntityType
                                     ? this.dspMetadata.AddEntityType(collectionName)
                                     : this.dspMetadata.AddComplexType(collectionName);

            bool hasObjectId = false;
            if (document != null)
            {
                foreach (var element in document.Elements)
                {
                    RegisterOrUpdateResourceProperty(context, collectionType, element);
                    if (IsObjectId(element))
                        hasObjectId = true;
                }
            }

            if (!hasObjectId)
            {
                if (resourceTypeKind == ResourceTypeKind.EntityType)
                {
                    this.dspMetadata.AddKeyProperty(collectionType, MappedObjectIdName, MappedObjectIdType);
                }
                else
                {
                    this.dspMetadata.AddPrimitiveProperty(collectionType, MappedObjectIdName, MappedObjectIdType);
                }
                AddProviderType(collectionName, ProviderObjectIdName, BsonObjectId.Empty, true);
            }

            if (resourceTypeKind == ResourceTypeKind.EntityType)
                this.dspMetadata.AddResourceSet(collectionName, collectionType);

            return collectionType;
        }

        private void UpdateResourceType(MongoContext context, ResourceType collectionType, BsonDocument document)
        {
            if (document != null)
            {
                foreach (var element in document.Elements)
                {
                    RegisterOrUpdateResourceProperty(context, collectionType, element);
                }
            }
        }

        internal void RegisterOrUpdateResourceProperty(MongoContext context, ResourceType resourceType, BsonElement element)
        {
            var resourceProperty = ResolveResourceProperty(resourceType, element);
            var elementType = GetElementType(element);
            if (resourceProperty == null)
            {
                RegisterResourceProperty(context, resourceType.Name, resourceType, elementType, element, true);
            }
            else
            {
                if (resourceProperty.ResourceType.InstanceType == typeof (object) && elementType != typeof (object))
                {
                    // TODO
//                    resourceProperty.ResourceType.InstanceType = elementType;
                }
            }
        }

        private void RegisterResourceProperty(MongoContext context, string collectionName, ResourceType collectionType,
            Type elementType, BsonElement element, bool treatObjectIdAsKey = false)
        {
            var propertyName = GetResourcePropertyName(element, collectionType.ResourceTypeKind);
            var propertyValue = element.Value;

            var collectionProperty = new CollectionProperty { CollectionType = collectionType, PropertyName = element.Name };
            if (ResolveProviderType(element.Value, IsObjectId(element)) == null)
            {
                if (!unresolvedProperties.Contains(collectionProperty))
                {
                    this.unresolvedProperties.Add(collectionProperty);
                }
                return;
            }
            else if (unresolvedProperties.Contains(collectionProperty))
            {
                unresolvedProperties.Remove(collectionProperty);
            }

            var isKey = false;
            if (IsObjectId(element))
            {
                if (treatObjectIdAsKey)
                    this.dspMetadata.AddKeyProperty(collectionType, propertyName, elementType);
                else
                    this.dspMetadata.AddPrimitiveProperty(collectionType, propertyName, elementType);
                isKey = true;
            }
            else if (elementType == typeof(BsonDocument))
            {
                RegisterDocumentProperty(context, collectionName, collectionType, propertyName, element);
            }
            else if (elementType == typeof(BsonArray))
            {
                RegisterArrayProperty(context, collectionName, collectionType, propertyName, element);
            }
            else
            {
                this.dspMetadata.AddPrimitiveProperty(collectionType, propertyName, elementType);
            }

            if (!string.IsNullOrEmpty(propertyName))
            {
                AddProviderType(collectionName, IsObjectId(element) ? ProviderObjectIdName : propertyName, propertyValue, isKey);
            }
        }

        private void UpdateComplexProperty(MongoContext context, ResourceType collectionType, BsonElement element)
        {
            var resourceName = GetResourcePropertyName(element, ResourceTypeKind.EntityType);
            var resourceType = ResolveResourceType(resourceName, collectionType.Name);
            UpdateResourceType(context, resourceType, element.Value.AsBsonDocument);
        }

        private void RegisterDocumentProperty(MongoContext context, string collectionName, ResourceType collectionType, string propertyName, BsonElement element, bool isCollection = false)
        {
            ResourceType resourceType = null;
            var resourceSet = ResolveResourceSet(collectionName);
            if (resourceSet != null)
            {
                resourceType = resourceSet.ResourceType;
            }
            else
            {
                resourceType = RegisterResourceType(context, GetQualifiedTypeName(collectionName, propertyName),
                                                    element.Value.AsBsonDocument, ResourceTypeKind.ComplexType);
            }
            if (isCollection)
                this.dspMetadata.AddCollectionProperty(collectionType, propertyName, resourceType);
            else
                this.dspMetadata.AddComplexProperty(collectionType, propertyName, resourceType);
        }

        private void RegisterArrayProperty(MongoContext context, string collectionName, ResourceType collectionType, string propertyName, BsonElement element)
        {
            var bsonArray = element.Value.AsBsonArray;
            if (bsonArray != null)
            {
                foreach (var arrayValue in bsonArray)
                {
                    if (arrayValue.AsBsonValue == BsonNull.Value)
                        continue;

                    if (arrayValue.BsonType == BsonType.Document)
                    {
                        var arrayElement = new BsonElement(element.Name, arrayValue);
                        ResourceType resourceType = ResolveResourceType(propertyName, collectionName);
                        if (resourceType == null)
                        {
                            RegisterDocumentProperty(context, collectionName, collectionType, propertyName, arrayElement, true);
                        }
                        else
                        {
                            UpdateComplexProperty(context, collectionType, arrayElement);
                        }
                    }
                    else
                    {
                        this.dspMetadata.AddCollectionProperty(collectionType, propertyName, arrayValue.RawValue.GetType());
                        break;
                    }
                }
            }
        }

        private void AddProviderType(string collectionName, string elementName, BsonValue elementValue, bool isKey = false)
        {
            Type providerType = ResolveProviderType(elementValue, isKey);
            if (providerType != null)
            {
                this.providerTypes.Add(string.Join(".", collectionName, elementName), providerType);
            }
        }

        private static Type ResolveProviderType(BsonValue elementValue, bool isKey)
        {
            if (elementValue.GetType() == typeof(BsonArray) || elementValue.GetType() == typeof(BsonDocument))
            {
                return elementValue.GetType();
            }
            else if (elementValue.RawValue != null)
            {
                return GetRawValueType(elementValue, isKey);
            }
            else
            {
                return null;
            }
        }

        public static bool IsObjectId(BsonElement element)
        {
            return element.Name == MongoMetadata.ProviderObjectIdName;
        }

        private static Type GetElementType(BsonElement element)
        {
            if (IsObjectId(element))
            {
                if (element.Value.GetType() == ProviderObjectIdType)
                    return MappedObjectIdType;
                else
                    return GetRawValueType(element.Value, true);
            }
            else if (element.Value.RawValue != null)
            {
                return GetRawValueType(element.Value, IsObjectId(element));
            }
            else if (element.Value.GetType() == typeof(BsonArray) || element.Value.GetType() == typeof(BsonDocument))
            {
                return element.Value.GetType();
            }
            else
            {
                switch (element.Value.BsonType)
                {
                    case BsonType.Null:
                        return typeof(object);
                    case BsonType.Binary:
                        return typeof(byte[]);
                    default:
                        return typeof(string);
                }
            }
        }

        private static Type GetRawValueType(BsonValue elementValue, bool isKey = false)
        {
            Type elementType;
            switch (elementValue.BsonType)
            {
                case BsonType.DateTime:
                    elementType = typeof(DateTime);
                    break;
                default:
                    elementType = elementValue.RawValue.GetType();
                    break;
            }
            if (!isKey && elementType.IsValueType)
            {
                elementType = typeof(Nullable<>).MakeGenericType(elementType);
            }
            return elementType;
        }

        internal static string GetQualifiedTypeName(string collectionName, string resourceName)
        {
            return UseGlobalComplexTypeNames ? resourceName : string.Join(WordSeparator, collectionName, resourceName);
        }

        internal static string GetQualifiedTypePrefix(string ownerName)
        {
            return UseGlobalComplexTypeNames ? string.Empty : ownerName;
        }

        internal static string GetResourcePropertyName(BsonElement element, ResourceTypeKind resourceTypeKind)
        {
            return IsObjectId(element) && resourceTypeKind != ResourceTypeKind.ComplexType ?
                MongoMetadata.MappedObjectIdName :
                NormalizeResourcePropertyName(element.Name);
        }

        internal static string NormalizeResourcePropertyName(string propertyName)
        {
            return propertyName.StartsWith("_") ? PrefixForInvalidLeadingChar + propertyName : propertyName;
        }
    }
}
