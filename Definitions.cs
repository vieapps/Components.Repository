#region Related components
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Dynamic;
using System.Data;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MongoDB.Bson.Serialization.Attributes;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Definition of a respository
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class RepositoryDefinition
	{
		public RepositoryDefinition()
		{
			this.IsAlias = false;
			this.ExtraSettings = new Dictionary<string, object>();
			this.RuntimeRepositories = new Dictionary<string, IRepository>();
		}

		#region Properties
		/// <summary>
		/// Gets the type of a class that responsibility of the repository
		/// </summary>
		public Type Type { get; internal set; }

		/// <summary>
		/// Gets the type of a class that responsibility to process all event handlers of the repository
		/// </summary>
		public Type EventHandlers { get; internal set; }

		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; internal set; }

		/// <summary>
		/// Gets that state that specified this repository is an alias of other repository
		/// </summary>
		public bool IsAlias { get; internal set; }

		/// <summary>
		/// Gets the extra-settings of the repository
		/// </summary>
		public Dictionary<string, object> ExtraSettings { get; internal set; }
		#endregion

		#region Properties [Helpers]
		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.PrimaryDataSourceName) && RepositoryMediator.DataSources.ContainsKey(this.PrimaryDataSourceName)
					? RepositoryMediator.DataSources[this.PrimaryDataSourceName]
					: null;
			}
		}
		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.SecondaryDataSourceName) && RepositoryMediator.DataSources.ContainsKey(this.SecondaryDataSourceName)
					? RepositoryMediator.DataSources[this.SecondaryDataSourceName]
					: null;
			}
		}
		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public List<DataSource> SyncDataSources
		{
			get
			{
				if (string.IsNullOrWhiteSpace(this.SyncDataSourceNames))
					return new List<DataSource>();
				else
				{
					HashSet<string> dataSources = this.SyncDataSourceNames.ToHashSet(',');
					return RepositoryMediator.DataSources
						.Where(item => dataSources.Contains(item.Key))
						.Select(item => item.Value)
						.ToList();
				}
			}
		}
		/// <summary>
		/// Gets the definitions of all entities
		/// </summary>
		public List<EntityDefinition> EntityDefinitions
		{
			get
			{
				return RepositoryMediator.EntityDefinitions
					.Where(item => item.Value.RepositoryTypeName.Equals(this.Type.GetTypeName()))
					.Select(item => item.Value)
					.ToList();
			}
		}
		#endregion

		#region Properties [Module Definition]
		/// <summary>
		/// Gets the identity (when this object is defined as a module definition)
		/// </summary>
		public string ID { get; internal set; }

		/// <summary>
		/// Gets the path to folder that contains all UI files (when this object is defined as a module definition)
		/// </summary>
		public string Path { get; internal set; }

		/// <summary>
		/// Gets the title (when this object is defined as a module definition)
		/// </summary>
		public string Title { get; internal set; }

		/// <summary>
		/// Gets the description (when this object is defined as a module definition)
		/// </summary>
		public string Description { get; internal set; }

		/// <summary>
		/// Gets the name of the SQL table for storing extended properties, default is 'T_Data_Extended_Properties' (when this object is defined as a module definition)
		/// </summary>
		public string ExtendedPropertiesTableName { get; internal set; }

		/// <summary>
		/// Gets the collection of business repositories at run-time (means business modules at run-time)
		/// </summary>
		public Dictionary<string, IRepository> RuntimeRepositories { get; internal set; }
		#endregion

		#region Register
		internal static void Register(Type type)
		{
			// check existed
			if (RepositoryMediator.RepositoryDefinitions.ContainsKey(type.GetTypeName()))
				return;

			// initialize
			var info = type.GetCustomAttributes(false)
				.Where(attribute => attribute is RepositoryAttribute)
				.ToList()[0] as RepositoryAttribute;

			var definition = new RepositoryDefinition()
			{
				Type = type,
				EventHandlers = info.EventHandlers,
				ID = !string.IsNullOrWhiteSpace(info.ID) ? info.ID : "",
				Path = !string.IsNullOrWhiteSpace(info.Path) ? info.Path : "",
				Title = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "",
				Description = !string.IsNullOrWhiteSpace(info.Description) ? info.Description : "",
				ExtendedPropertiesTableName = !string.IsNullOrWhiteSpace(info.ExtendedPropertiesTableName) ? info.ExtendedPropertiesTableName : "T_Data_Extended_Properties"
			};

			// check type of event-handlers
			definition.EventHandlers = definition.EventHandlers != null && definition.EventHandlers.CreateInstance() != null
				? definition.EventHandlers
				: null;

			// update into collection
			RepositoryMediator.RepositoryDefinitions.Add(type.GetTypeName(), definition);
		}
		#endregion

		#region Update settings
		internal static void Update(JObject settings)
		{
			// check settings
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			var isAlias = settings["isAlias"] != null
				? ((settings["isAlias"] as JValue).Value as string).IsEquals("true")
				: false;

			Type targetType = null;
			if (isAlias)
			{
				var target = settings["target"] != null
					? (settings["target"] as JValue).Value as string
					: null;

				if (string.IsNullOrWhiteSpace(target))
					isAlias = false;
				else
				{
					targetType = Type.GetType(target);
					if (targetType == null)
						isAlias = false;
				}
			}

			// check type
			var type = isAlias
				? targetType
				: Type.GetType((settings["type"] as JValue).Value as string);
			if (type == null)
				return;

			// get type name
			var typeName = isAlias
				? (settings["type"] as JValue).Value as string
				: type.GetTypeName();

			// clone definition for new alias
			if (isAlias && !RepositoryMediator.RepositoryDefinitions.ContainsKey(typeName))
			{
				if (!RepositoryMediator.RepositoryDefinitions.ContainsKey(type.GetTypeName()))
					throw new ArgumentException("The target type named '" + type.GetTypeName() + "' is not available");

				RepositoryMediator.RepositoryDefinitions.Add(typeName, RepositoryMediator.RepositoryDefinitions[type.GetTypeName()].Clone());
				RepositoryMediator.RepositoryDefinitions[typeName].IsAlias = true;
				RepositoryMediator.RepositoryDefinitions[typeName].ExtraSettings = RepositoryMediator.RepositoryDefinitions[type.GetTypeName()].ExtraSettings;
			}

			// check existing
			if (!RepositoryMediator.RepositoryDefinitions.ContainsKey(typeName))
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			if (string.IsNullOrEmpty(data))
				throw new ArgumentNullException("primaryDataSource", "[primaryDataSource] attribute of settings");
			else if (!RepositoryMediator.DataSources.ContainsKey(data))
				throw new ArgumentException("The data source named '" + data + "' is not available");
			RepositoryMediator.RepositoryDefinitions[typeName].PrimaryDataSourceName = data;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data) && !RepositoryMediator.DataSources.ContainsKey(data))
				data = null;
			RepositoryMediator.RepositoryDefinitions[typeName].SecondaryDataSourceName = data;

			data = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data))
			{
				if (data.Equals(","))
					data = null;
				else
				{
					var names = data.ToArray(',', true, true);
					data = "";
					names.ForEach(name =>
					{
						data += RepositoryMediator.DataSources.ContainsKey(name)
							? (!data.Equals("") ? "," : "") + name
							: "";
					});
				}
			}
			RepositoryMediator.RepositoryDefinitions[typeName].SyncDataSourceNames = data;
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Definition of an entity in a respository
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class EntityDefinition
	{
		public EntityDefinition()
		{
			this.Attributes = new List<ObjectService.AttributeInfo>();
			this.SortableAttributes = new List<string>() { "ID" };
			this.Searchable = true;
			this.ExtraSettings = new Dictionary<string, object>();
			this.Indexable = true;
			this.MultipleIntances = false;
			this.Extendable = false;
			this.MultipleParentAssociates = false;
			this.RuntimeEntities = new Dictionary<string, IRepositoryEntity>();
		}

		#region Properties
		/// <summary>
		/// Gets the type of the object for processing
		/// </summary>
		public Type Type { get; internal set; }

		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; internal set; }

		/// <summary>
		/// Gets or sets the name of the table in SQL database
		/// </summary>
		public string TableName { get; internal set; }

		/// <summary>
		/// Gets or sets the name of the collection in NoSQL database
		/// </summary>
		public string CollectionName { get; internal set; }

		/// <summary>
		/// Gets the type of a static class that contains information of the cache storage for processing caching data
		/// </summary>
		public Type CacheStorageType { get; internal set; }

		/// <summary>
		/// Gets the name of the object in the static class that contains information of the cache storage for processing caching data
		/// </summary>
		public string CacheStorageName { get; internal set; }

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to search using full-text method
		/// </summary>
		public bool Searchable { get; internal set; }

		/// <summary>
		/// Gets extra settings of of the entity definition
		/// </summary>
		public Dictionary<string, object> ExtraSettings { get; internal set; }
		#endregion

		#region Properties [Helpers]
		/// <summary>
		/// Gets the cache storage object for processing caching data of this entity
		/// </summary>
		public Caching.CacheManager CacheStorage { get; internal set; }

		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.PrimaryDataSourceName) && RepositoryMediator.DataSources.ContainsKey(this.PrimaryDataSourceName)
					? RepositoryMediator.DataSources[this.PrimaryDataSourceName]
					: null;
			}
		}

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource
		{
			get
			{
				return !string.IsNullOrWhiteSpace(this.SecondaryDataSourceName) && RepositoryMediator.DataSources.ContainsKey(this.SecondaryDataSourceName)
					? RepositoryMediator.DataSources[this.SecondaryDataSourceName]
					: null;
			}
		}

		/// <summary>
		/// Gets the other data sources that are available for synchronizing
		/// </summary>
		public List<DataSource> SyncDataSources
		{
			get
			{
				if (string.IsNullOrWhiteSpace(this.SyncDataSourceNames))
					return new List<DataSource>();
				else
				{
					HashSet<string> dataSources = this.SyncDataSourceNames.ToHashSet();
					return RepositoryMediator.DataSources
						.Where(item => dataSources.Contains(item.Key))
						.Select(item => item.Value)
						.ToList();
				}
			}
		}

		/// <summary>
		/// Gets the collection of all attributes (properties and fields)
		/// </summary>
		public List<ObjectService.AttributeInfo> Attributes { get; internal set; }

		internal string PrimaryKey { get; set; }

		/// <summary>
		/// Gets the information of the primary key
		/// </summary>
		public ObjectService.AttributeInfo PrimaryKeyInfo
		{
			get
			{
				return this.Attributes.FirstOrDefault(attribute => attribute.Name.Equals(this.PrimaryKey));
			}
		}

		/// <summary>
		/// Gets the collection of sortable properties
		/// </summary>
		public List<string> SortableAttributes { get; internal set; }

		internal string RepositoryTypeName { get; set; }

		/// <summary>
		/// Gets the repository definition of this defintion
		/// </summary>
		public RepositoryDefinition RepositoryDefinition
		{
			get
			{
				return RepositoryMediator.GetRepositoryDefinition(this.RepositoryTypeName);
			}
		}
		#endregion

		#region Properties [Content-Type Definition]
		/// <summary>
		/// Gets the identity (when this object is defined as a content-type definition)
		/// </summary>
		public string ID { get; internal set; }

		/// <summary>
		/// Gets the title (when this object is defined as a content-type definition)
		/// </summary>
		public string Title { get; internal set; }

		/// <summary>
		/// Gets the description (when this object is defined as a content-type definition)
		/// </summary>
		public string Description { get; internal set; }

		/// <summary>
		/// Gets the state that allow to use multiple instances, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleIntances { get; internal set; }

		/// <summary>
		/// Gets the state that allow to extend this entity by extended properties, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Extendable { get; internal set; }

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to index with global search module, default is true (when this object is defined as a content-type definition)
		/// </summary>
		public bool Indexable { get; internal set; }

		/// <summary>
		/// Gets the type of parent entity definition (when this object is defined as a content-type definition)
		/// </summary>
		public Type ParentType { get; internal set; }

		/// <summary>
		/// Gets the name of the property that use to associate with parent object (when this object is defined as a content-type definition)
		/// </summary>
		public string ParentAssociatedProperty { get; internal set; }

		/// <summary>
		/// Gets the state that specifies this entity had multiple associates with parent object, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleParentAssociates { get; internal set; }

		/// <summary>
		/// Gets the name of the property that use to store the information of multiple associates with parent, mus be List or HashSet (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesProperty { get; internal set; }

		/// <summary>
		/// Gets the name of the SQL table that use to store the information of multiple associates with parent (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesTable { get; internal set; }

		/// <summary>
		/// Gets the name of the column of SQL table that use to map the associate with parent (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesMapColumn { get; internal set; }

		/// <summary>
		/// Gets the name of the column of SQL table that use to link the associate with this entity (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesLinkColumn { get; internal set; }

		/// <summary>
		/// Gets the name of the property to use as short-name (when this object is defined as a content-type definition)
		/// </summary>
		public string ShortnameProperty { get; internal set; }

		/// <summary>
		/// Gets the type of a class that use to generate navigator menu (when this object is defined as a content-type definition)
		/// </summary>
		public Type NavigatorType { get; internal set; }

		/// <summary>
		/// Gets the collection of business entities at run-time (means business conten-types at run-time)
		/// </summary>
		public Dictionary<string, IRepositoryEntity> RuntimeEntities { get; internal set; }
		#endregion

		#region Register
		internal static void Register(Type type)
		{
			// check existed
			if (RepositoryMediator.EntityDefinitions.ContainsKey(type.GetTypeName()))
				return;

			// check table/collection name
			var info = type.GetCustomAttributes(false).FirstOrDefault(attribute => attribute is EntityAttribute) as EntityAttribute;
			if (string.IsNullOrWhiteSpace(info.TableName) && string.IsNullOrWhiteSpace(info.CollectionName))
				throw new ArgumentException("The type [" + type.ToString() + "] must have name of SQL table or NoSQL collection");

			// initialize
			var definition = new EntityDefinition()
			{
				Type = type,
				TableName = info.TableName,
				CollectionName = info.CollectionName,
				CacheStorageType = info.CacheStorageType,
				CacheStorageName = info.CacheStorageName,
				Searchable = info.Searchable,
				ID = !string.IsNullOrWhiteSpace(info.ID) ? info.ID : "",
				Title = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : "",
				Description = !string.IsNullOrWhiteSpace(info.Description) ? info.Description : "",
				MultipleIntances = info.MultipleIntances,
				Extendable = info.Extendable,
				Indexable = info.Indexable,
				ParentType = info.ParentType,
				NavigatorType = info.NavigatorType
			};

			// set cache storage
			if (definition.CacheStorageType != null && !string.IsNullOrWhiteSpace(definition.CacheStorageName))
			{
				var cacheStorage = definition.CacheStorageType.GetStaticObject(definition.CacheStorageName);
				definition.CacheStorage = cacheStorage != null && cacheStorage is Caching.CacheManager
					? cacheStorage as Caching.CacheManager
					: null;
			}

			// parent (repository)
			var parent = type.BaseType;
			var grandparent = parent.BaseType;
			while (grandparent.BaseType != null && grandparent.BaseType != typeof(object) && grandparent.BaseType != typeof(RepositoryBase))
			{
				parent = parent.BaseType;
				grandparent = parent.BaseType;
			}
			definition.RepositoryTypeName = parent.GetTypeName();
			definition.RepositoryTypeName = definition.RepositoryTypeName.Left(definition.RepositoryTypeName.IndexOf("[")) + definition.RepositoryTypeName.Substring(definition.RepositoryTypeName.IndexOf("]") + 2);

			// public properties
			var numberOfKeys = 0;
			var properties = ObjectService.GetProperties(type);
			properties.ForEach(attribute =>
			{
				if (!attribute.IsIgnored())
				{
					// primary key
					var attributes = attribute.Info.GetCustomAttributes(typeof(PrimaryKeyAttribute), true);
					if (attributes.Length > 0)
					{
						attribute.Column = (attributes[0] as PrimaryKeyAttribute).Column;
						attribute.NotNull = true;
						attribute.MaxLength = (attributes[0] as PrimaryKeyAttribute).MaxLength;
						attribute.IsCLOB = false;

						definition.PrimaryKey = attribute.Name;
						numberOfKeys += attributes.Length;
					}

					// property
					attributes = attribute.Info.GetCustomAttributes(typeof(PropertyAttribute), true);
					if (attributes.Length > 0)
					{
						attribute.Column = (attributes[0] as PropertyAttribute).Column;
						attribute.NotNull = (attributes[0] as PropertyAttribute).NotNull;
						attribute.MaxLength = (attributes[0] as PropertyAttribute).MaxLength;
						attribute.IsCLOB = (attributes[0] as PropertyAttribute).IsCLOB;
					}

					// sortable
					attributes = attribute.Info.GetCustomAttributes(typeof(SortableAttribute), true);
					if (attributes.Length > 0)
						definition.SortableAttributes.Add(attribute.Name);

					// update
					definition.Attributes.Add(attribute);
				}
			});

			// check primary key
			if (numberOfKeys == 0)
				throw new ArgumentException("The type [" + type.ToString() + "] has no primary-key");
			else if (numberOfKeys > 1)
				throw new ArgumentException("The type [" + type.ToString() + "] has multiple primary-keys");

			// fields
			ObjectService.GetFields(type).ForEach(attribute =>
			{
				var attributes = attribute.Info.GetCustomAttributes(typeof(FieldAttribute), false);
				if (attributes.Length > 0)
				{
					attribute.Column = (attributes[0] as FieldAttribute).Column;
					attribute.NotNull = (attributes[0] as FieldAttribute).NotNull;
					attribute.MaxLength = (attributes[0] as FieldAttribute).MaxLength;
					attribute.IsCLOB = (attributes[0] as FieldAttribute).IsCLOB;
					definition.Attributes.Add(attribute);
				}
			});

			// parent (entity)
			var parentEntity = definition.ParentType != null
				? definition.ParentType.CreateInstance()
				: null;

			if (parentEntity != null)
			{
				var property = string.IsNullOrWhiteSpace(info.ParentAssociatedProperty)
					? null
					: properties.FirstOrDefault(p => p.Name.Equals(info.ParentAssociatedProperty));
				definition.ParentAssociatedProperty = property != null
					? info.ParentAssociatedProperty
					: "";

				if (!string.IsNullOrWhiteSpace(definition.ParentAssociatedProperty))
				{
					definition.MultipleParentAssociates = info.MultipleParentAssociates;

					property = string.IsNullOrWhiteSpace(info.MultipleParentAssociatesProperty)
						? null
						: properties.FirstOrDefault(p => p.Name.Equals(info.MultipleParentAssociatesProperty));
					definition.MultipleParentAssociatesProperty = property != null && property.Type.IsGenericListOrHashSet()
						? info.MultipleParentAssociatesProperty
						: "";

					definition.MultipleParentAssociatesTable = !string.IsNullOrWhiteSpace(info.MultipleParentAssociatesTable)
						? info.MultipleParentAssociatesTable
						: "";

					if (!string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesTable))
					{
						definition.MultipleParentAssociatesMapColumn = !string.IsNullOrWhiteSpace(info.MultipleParentAssociatesMapColumn)
							? info.MultipleParentAssociatesMapColumn
							: "";

						definition.MultipleParentAssociatesLinkColumn = !string.IsNullOrWhiteSpace(info.MultipleParentAssociatesLinkColumn)
							? info.MultipleParentAssociatesLinkColumn
							: "";
					}
				}
			}
			else
				definition.ParentType = null;

			// shortname
			definition.ShortnameProperty = !string.IsNullOrWhiteSpace(info.ShortnameProperty) && properties.FirstOrDefault(p => p.Name.Equals(info.ShortnameProperty)) != null
				? info.ShortnameProperty
				: "";

			// navigator
			definition.NavigatorType = definition.NavigatorType != null && definition.NavigatorType.CreateInstance() != null
				? definition.NavigatorType
				: null;

			// update into collection
			RepositoryMediator.EntityDefinitions.Add(type.GetTypeName(), definition);
		}
		#endregion

		#region Update settings
		internal static void Update(JObject settings)
		{
			// check settings
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// check type
			var type = Type.GetType((settings["type"] as JValue).Value as string);
			if (type == null)
				return;

			var typeName = type.GetTypeName();
			if (!RepositoryMediator.EntityDefinitions.ContainsKey(typeName))
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data) && !RepositoryMediator.DataSources.ContainsKey(data))
				data = null;
			RepositoryMediator.EntityDefinitions[typeName].PrimaryDataSourceName = data;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data) && !RepositoryMediator.DataSources.ContainsKey(data))
				data = null;
			RepositoryMediator.EntityDefinitions[typeName].SecondaryDataSourceName = data;

			data = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;
			if (!string.IsNullOrEmpty(data))
			{
				if (data.Equals(","))
					data = null;
				else
				{
					var names = data.ToArray(',', true, true);
					data = "";
					names.ForEach(name =>
					{
						data += RepositoryMediator.DataSources.ContainsKey(name)
							? (!data.Equals("") ? "," : "") + name
							: "";
					});
				}
			}
			RepositoryMediator.EntityDefinitions[typeName].SyncDataSourceNames = data;

			// individual cache storage
			if (settings["cacheRegion"] != null)
			{
				var cacheRegion = (settings["cacheRegion"] as JValue).Value as string;
				var cacheExpirationType = settings["cacheExpirationType"] != null
					? (settings["cacheExpirationType"] as JValue).Value as string
					: "Sliding";
				var cacheExpirationTime = 30;
				if (settings["cacheExpirationTime"] != null)
					try
					{
						cacheExpirationTime = Convert.ToInt32((settings["cacheExpirationTime"] as JValue).Value);
						if (cacheExpirationTime < 0)
							cacheExpirationTime = 30;
					}
					catch { }
				var cacheActiveSynchronize = settings["cacheActiveSynchronize"] != null
					? ((settings["cacheActiveSynchronize"] as JValue).Value as string).IsEquals("true")
					: false;
				RepositoryMediator.EntityDefinitions[typeName].CacheStorage = new Caching.CacheManager(cacheRegion, cacheExpirationType.IsEquals("absolute") ? "Absolute" : "Sliding", cacheExpirationTime, cacheActiveSynchronize);
			}
		}

		/// <summary>
		/// Sets the cache storage of an entity definition
		/// </summary>
		/// <param name="type">The type that presents information of an entity definition</param>
		/// <param name="cacheStorage">The cache storage</param>
		public void SetCacheStorage(Type type, Caching.CacheManager cacheStorage)
		{
			if (type != null && RepositoryMediator.EntityDefinitions.ContainsKey(type.GetTypeName()))
				RepositoryMediator.EntityDefinitions[type.GetTypeName()].CacheStorage = cacheStorage;
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Information of a definition of extended property in a respository 
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
	public sealed class ExtendedPropertyDefinition
	{

		public ExtendedPropertyDefinition(JObject json = null)
		{
			this.Name = "";
			this.Mode = ExtendedPropertyMode.Text;
			this.Column = "";
			this.DefaultValue = "";
			this.DefaultValueFormula = "";

			if (json != null)
				this.CopyFrom(json);
		}

		#region Properties
		/// <summary>
		/// Gets or sets the name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the mode
		/// </summary>
		public ExtendedPropertyMode Mode { get; set; }

		/// <summary>
		/// Gets or sets the name of column for storing data (when repository mode is SQL)
		/// </summary>
		public string Column { get; set; }

		/// <summary>
		/// Gets or sets the default value
		/// </summary>
		public string DefaultValue { get; set; }

		/// <summary>
		/// Gets or sets the formula for computing default value
		/// </summary>
		public string DefaultValueFormula { get; set; }
		#endregion

		#region Properties [Helper]
		/// <summary>
		/// Gets the type of this property
		/// </summary>
		[JsonIgnore, BsonIgnore, XmlIgnore]
		internal Type Type
		{
			get
			{
				switch (this.Mode)
				{
					case ExtendedPropertyMode.YesNo:
						return typeof(Int32);

					case ExtendedPropertyMode.Number:
						return typeof(Int64);

					case ExtendedPropertyMode.Decimal:
						return typeof(Decimal);

					case ExtendedPropertyMode.DateTime:
						return typeof(DateTime);
				}
				return typeof(String);
			}
		}

		/// <summary>
		/// Gets the database-type of this property
		/// </summary>
		[JsonIgnore, BsonIgnore, XmlIgnore]
		internal DbType DbType
		{
			get
			{
				switch (this.Mode)
				{
					case ExtendedPropertyMode.YesNo:
						return SqlHelper.DbTypes[typeof(Int32)];

					case ExtendedPropertyMode.Number:
						return SqlHelper.DbTypes[typeof(Int64)];

					case ExtendedPropertyMode.Decimal:
						return SqlHelper.DbTypes[typeof(Decimal)];

					case ExtendedPropertyMode.DateTime:
						return DbType.AnsiString;
				}
				return SqlHelper.DbTypes[typeof(String)];
			}
		}
		#endregion

		#region Parse XML (backward compatible)
		/*
		public void Parse(XElement element)
		{
			try
			{
				var node = element.Element(XName.Get("Mode"));
				var mode = node != null ? node.Value : "";

				node = element.Element(XName.Get("Name"));
				this.Name = node != null ? node.Value : this.Name;

				node = element.Element(XName.Get("Column"));
				this.Column = node != null ? node.Value : this.Column;

				node = element.Element(XName.Get("DefaultValue"));
				this.DefaultValue = node != null ? node.Value : this.DefaultValue;

				node = element.Element(XName.Get("Label"));
				this.UI.Label = node != null ? node.Value : this.UI.Label;

				node = element.Element(XName.Get("Description"));
				this.UI.Description = node != null ? node.Value : this.UI.Description;

				node = element.Element(XName.Get("Required"));
				this.UI.Required = node != null ? node.Value.IsEquals("true") : this.UI.Required;

				node = element.Element(XName.Get("MaxLength"));
				this.UI.MaxLength = node != null ? node.Value.CastType<int>() : this.UI.MaxLength;

				switch (mode)
				{
					// text
					case "0":
					case "1":
						this.Mode = CustomPropertyMode.Text;
						this.UI.AllowMultiple = mode.Equals("1");
						break;

					// text (large)
					case "2":
						this.Mode = CustomPropertyMode.LargeText;
						this.UI.UseAsTextEditor = true;
						node = element.Element(XName.Get("TextEditorWidth"));
						this.UI.TextEditorWidth = node != null ? node.Value : this.UI.TextEditorWidth;
						node = element.Element(XName.Get("TextEditorHeight"));
						this.UI.TextEditorHeight = node != null ? node.Value : this.UI.TextEditorHeight;
						break;

					// yes/no
					case "3":
						this.Mode = CustomPropertyMode.YesNo;
						this.DefaultValue = "Yes";
						this.UI.PredefinedValues = "Yes\nNo";
						break;

					// choice
					case "4":
					case "5":
						this.Mode = CustomPropertyMode.Choice;
						this.UI.AllowMultiple = mode.Equals("5");
						node = element.Element(XName.Get("ChoiceStyle"));
						if (mode.Equals("4"))
							this.UI.Format = node != null && node.Value.Equals("0") ? "Radio" : "Select";
						else
							this.UI.Format = node != null && node.Value.Equals("0") ? "Checkbox" : "Listbox";
						node = element.Element(XName.Get("ChoiceValues"));
						this.UI.PredefinedValues = node != null ? node.Value : this.UI.PredefinedValues;
						break;

					// date-time
					case "6":
						this.Mode = CustomPropertyMode.DateTime;
						node = element.Element(XName.Get("DateTimeValueFormat"));
						this.UI.Format = node != null ? node.Value : "dd/MM/yyyy";
						break;

					// number (integer)
					case "7":
					case "8":
						this.Mode = CustomPropertyMode.Number;
						node = element.Element(XName.Get("NumberMinValue"));
						this.UI.MinValue = node != null && node.Value.CastType<int>() > -1 ? node.Value.CastType<int>().ToString() : this.UI.MinValue;
						node = element.Element(XName.Get("NumberMaxValue"));
						this.UI.MaxValue = node != null && node.Value.CastType<int>() > -1 ? node.Value.CastType<int>().ToString() : this.UI.MaxValue;
						node = element.Element(XName.Get("DisplayFormat"));
						this.UI.Format = node != null ? node.Value : this.UI.Format;
						break;

					// number (decimal)
					case "9":
						this.Mode = CustomPropertyMode.Decimal;
						node = element.Element(XName.Get("DisplayFormat"));
						this.UI.Format = node != null ? node.Value : this.UI.Format;
						break;

					// hyper-link
					case "10":
						this.Mode = CustomPropertyMode.HyperLink;
						break;

					// look-up
					case "11":
						this.Mode = CustomPropertyMode.Lookup;
						node = element.Element(XName.Get("LookupModuleId"));
						this.UI.LookupRepositoryID = node != null ? node.Value : this.UI.LookupRepositoryID;
						node = element.Element(XName.Get("LookupContentTypeId"));
						this.UI.LookupEntityID = node != null ? node.Value : this.UI.LookupEntityID;
						node = element.Element(XName.Get("LookupColumn"));
						this.UI.LookupProperty = node != null ? node.Value : this.UI.LookupProperty;
						node = element.Element(XName.Get("LookupMultiple"));
						this.UI.AllowMultiple = node != null ? node.Value.IsEquals("true") : this.UI.AllowMultiple;
						break;

					// user
					case "12":
						this.Mode = CustomPropertyMode.User;
						node = element.Element(XName.Get("LookupMultiple"));
						this.UI.AllowMultiple = node != null ? node.Value.IsEquals("true") : this.UI.AllowMultiple;
						break;

					default:
						this.Mode = CustomPropertyMode.Text;
						this.UI.MaxLength = 250;
						this.UI.UseAsTextEditor = true;
						break;
				}
			}
			catch { }
		}
		*/
		#endregion

		#region Name validation
		static HashSet<string> ReservedWords = "ExtendedProperties,Add,External,Procedure,All,Fetch,Public,Alter,File,RaisError,And,FillFactor,Read,Any,For,ReadText,As,Foreign,ReConfigure,Asc,FreeText,References,Authorization,FreeTextTable,Replication,Backup,From,Restore,Begin,Full,Restrict,Between,Function,Return,Break,Goto,Revert,Browse,Grant,Revoke,Bulk,Group,Right,By,Having,Rollback,Cascade,Holdlock,Rowcount,Case,Identity,RowGuidCol,Check,Identity,Insert,Rule,Checkpoint,Identitycol,Save,Close,If,Schema,Clustered,In,SecurityAudit,Coalesce,Index,Select,Collate,Inner,SemanticKeyPhraseTable,Column,SemanticSimilarityDetailsTable,Commit,Intersect,SemanticSimilarityTable,Compute,Into,Session,User,Constraint,Is,Set,Contains,Join,Setuser,ContainsTable,Key,Shutdown,Continue,Kill,Some,Convert,Left,Statistics,Create,Like,System,Cross,Lineno,Table,Current,Load,TableSample,Current_Date,Current_Time,Current_Timestamp,Merge,TextSize,National,Then,NoCheck,To,Current_User,NonClustered,Top,Cursor,Not,Tran,Database,Null,Transaction,Dbcc,NullIf,Trigger,Deallocate,Of,Truncate,Declare,Off,Try_Convert,Default,Offsets,Tsequal,Delete,On,Union,Deny,Open,Unique,Desc,OpenDataSource,Unpivot,Disk,Openquery,Update,Distinct,OpenRowset,UpdateText,Distributed,OpenXml,Use,Double,Option,User,Drop,Or,Values,Dump,Order,Varying,Else,Outer,View,End,Over,Waitfor,Errlvl,Percent,When,Escape,Pivot,Where,Except,Plan,While,Exec,Precision,With,Execute,Primary,Exists,Print,WriteText,Exit,Proc".ToLower().ToHashSet();

		/// <summary>
		/// Validates the name of a custom property definition
		/// </summary>
		/// <param name="name">The string that presents name of a custom property</param>
		/// <remarks>An exception will be thrown if the name is invalid</remarks>
		public static void Validate(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException("name", "The name is null or empty");

			var validName = name.GetANSIUri().Replace("-", "");
			if (!validName.Equals(name))
				throw new InformationInvalidException("The name is contains one or more invalid characters (like space, -, +, ...)");

			if (name.ToUpper()[0] < 'A' || name.ToUpper()[0] > 'Z')
				throw new InformationInvalidException("The name must starts with a letter");

			if (ExtendedPropertyDefinition.ReservedWords.Contains(name.ToLower()))
				throw new InformationInvalidException("The name is system reserved word");
		}

		/// <summary>
		/// Validates the name of a custom property definition
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The string that presents name of a custom property</param>
		/// <remarks>An exception will be thrown if the name is invalid</remarks>
		public static void Validate<T>(string name) where T : class
		{
			ExtendedPropertyDefinition.Validate(name);
			var attributes = ObjectService.GetProperties(RepositoryMediator.GetEntityDefinition<T>().Type).ToDictionary(info => info.Name.ToLower(), info => info.Name);
			if (attributes.ContainsKey(name.ToLower()))
				throw new InformationInvalidException("The name is already used");
		}
		#endregion

		#region Helper methods
		public object GetDefaultValue()
		{
			return null;
		}

		public override string ToString()
		{
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// UI definition for working with a custom property in a respository 
	/// </summary>
	[Serializable]
	public sealed class ExtendedUIDefinition
	{

		public ExtendedUIDefinition(JObject json = null)
		{
			this.Controls = new List<ExtendedUIControlDefinition>();
			this.EditXslt = "";
			this.ListXslt = "";
			this.ViewXslt = "";

			if (json != null)
				this.CopyFrom(json);
		}

		#region Properties
		public List<ExtendedUIControlDefinition> Controls { get; set; }

		public string EditXslt { get; set; }

		public string ListXslt { get; set; }

		public string ViewXslt { get; set; }
		#endregion

		public override string ToString()
		{
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
		}

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// UI definition of a custom property in a respository 
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}")]
	public sealed class ExtendedUIControlDefinition
	{

		public ExtendedUIControlDefinition(JObject json = null)
		{
			this.Name = "";
			this.Label = "";
			this.PlaceHolder = "";
			this.Description = "";
			this.CssClass = "";
			this.CssMinWidth = "";
			this.CssMinHeight = "";
			this.MaxLength = 0;
			this.MinValue = "";
			this.MaxValue = "";
			this.UseAsTextEditor = false;
			this.TextEditorWidth = "";
			this.TextEditorHeight = "";
			this.Format = "";
			this.PredefinedValues = "";
			this.LookupRepositoryID = "";
			this.LookupEntityID = "";
			this.LookupProperty = "";
			this.Shown = true;
			this.Required = false;
			this.AllowMultiple = false;

			if (json != null)
				this.CopyFrom(json);
		}

		#region Properties
		/// <summary>
		/// Gets or sets the name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the label
		/// </summary>
		public string Label { get; set; }

		/// <summary>
		/// Gets or sets the place-holder
		/// </summary>
		public string PlaceHolder { get; set; }

		/// <summary>
		/// Gets or sets the description
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the CSS class name
		/// </summary>
		public string CssClass { get; set; }

		/// <summary>
		/// Gets or sets the CSS min-width
		/// </summary>
		public string CssMinWidth { get; set; }

		/// <summary>
		/// Gets or sets the CSS min-height
		/// </summary>
		public string CssMinHeight { get; set; }

		/// <summary>
		/// Gets or sets the max-length of input control or max-length of text (when the property is text)
		/// </summary>
		public int MaxLength { get; set; }

		/// <summary>
		/// Gets or sets the state that mark this property is use text-editor (when the property is large text)
		/// </summary>
		public bool UseAsTextEditor { get; set; }

		/// <summary>
		/// Gets or sets the width of text-editor (when the property is large text and marks to use text editor)
		/// </summary>
		public string TextEditorWidth { get; set; }

		/// <summary>
		/// Gets or sets the height of text-editor (when the property is large text and marks to use text editor)
		/// </summary>
		public string TextEditorHeight { get; set; }

		/// <summary>
		/// Gets or sets the minimum value (when the property is number or date-time)
		/// </summary>
		public string MinValue { get; set; }

		/// <summary>
		/// Gets or sets the maximum value (when the property is number or date-time)
		/// </summary>
		public string MaxValue { get; set; }

		/// <summary>
		/// Gets or sets the displaying format (when the property is number/date-time or choice)
		/// </summary>
		public string Format { get; set; }

		/// <summary>
		/// Gets or sets the pre-defined values (when the property is choice) - seperated by new line (LF)
		/// </summary>
		public string PredefinedValues { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business repository (when the property is lookup)
		/// </summary>
		public string LookupRepositoryID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business entity (when the property is lookup)
		/// </summary>
		public string LookupEntityID { get; set; }

		/// <summary>
		/// Gets or sets the name of business entity's property for displaying (when the property is lookup)
		/// </summary>
		public string LookupProperty { get; set; }

		/// <summary>
		/// Gets or sets the state that mark this property is shown for inputing
		/// </summary>
		public bool Shown { get; set; }

		/// <summary>
		/// Gets or sets the state that mark this property is required
		/// </summary>
		public bool Required { get; set; }

		/// <summary>
		/// Gets or sets the state that allow multiple values (when the property is text, choice or lookup)
		/// </summary>
		public bool AllowMultiple { get; set; }
		#endregion

		public override string ToString()
		{
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
		}

	}
}