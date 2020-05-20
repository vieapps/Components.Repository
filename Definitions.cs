#region Related components
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
using net.vieapps.Components.Caching;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Presents a respository definition (means a module definition)
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class RepositoryDefinition
	{
		public RepositoryDefinition() { }

		#region Properties
		/// <summary>
		/// Gets the type of the class that responsibility to process data of the repository
		/// </summary>
		public Type Type { get; internal set; }

		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource => RepositoryMediator.GetDataSource(this.PrimaryDataSourceName);

		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource => RepositoryMediator.GetDataSource(this.SecondaryDataSourceName);

		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; internal set; }

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public List<DataSource> SyncDataSources
			=> string.IsNullOrWhiteSpace(this.SyncDataSourceNames)
				? new List<DataSource>()
				: this.SyncDataSourceNames.ToList()
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Select(name => RepositoryMediator.GetDataSource(name))
					.Where(dataSource => dataSource != null)
					.ToList();

		/// <summary>
		/// Gets the name of the data source for storing information of versioning contents
		/// </summary>
		public string VersionDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the data-source that use to store versioning contents
		/// </summary>
		public DataSource VersionDataSource => RepositoryMediator.GetDataSource(this.VersionDataSourceName);

		/// <summary>
		/// Gets the name of the data source for storing information of trash contents
		/// </summary>
		public string TrashDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the data-source that use to store trash contents
		/// </summary>
		public DataSource TrashDataSource => RepositoryMediator.GetDataSource(this.TrashDataSourceName);

		/// <summary>
		/// Gets that state that specified this repository is an alias of other repository
		/// </summary>
		public bool IsAlias { get; internal set; } = false;

		/// <summary>
		/// Gets that state that specified data of this repository is sync automatically between data sources
		/// </summary>
		public bool AutoSync { get; internal set; } = false;

		/// <summary>
		/// Gets the extra information of the repository definition
		/// </summary>
		public Dictionary<string, object> Extras { get; internal set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the definitions of all entities
		/// </summary>
		public List<EntityDefinition> EntityDefinitions => RepositoryMediator.EntityDefinitions.Where(kvp => kvp.Value.RepositoryDefinitionType.Equals(this.Type)).Select(kvp => kvp.Value).ToList();
		#endregion

		#region Properties [Module Definition]
		/// <summary>
		/// Gets the name of the service that associates with the repository (when this object is defined as a module definition)
		/// </summary>
		public string ServiceName { get; internal set; }

		/// <summary>
		/// Gets the identity (when this object is defined as a module definition)
		/// </summary>
		public string ID { get; internal set; }

		/// <summary>
		/// Gets the title (when this object is defined as a module definition)
		/// </summary>
		public string Title { get; internal set; }

		/// <summary>
		/// Gets the description (when this object is defined as a module definition)
		/// </summary>
		public string Description { get; internal set; }

		/// <summary>
		/// Gets or sets the name of the icon for working with user interfaces (when this object is defined as a module definition)
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// Gets or sets the name of the directory that contains all files for working with user interfaces (when this object is defined as a module definition - will be placed in directory named '/themes/modules/', the value of 'ServiceName' will be used if no value was provided)
		/// </summary>
		public string Directory { get; internal set; }

		/// <summary>
		/// Gets the name of the SQL table for storing extended properties, default is 'T_Data_Extended_Properties' (when this object is defined as a module definition)
		/// </summary>
		public string ExtendedPropertiesTableName { get; internal set; }

		/// <summary>
		/// Gets the collection of business repositories (means business modules at run-time)
		/// </summary>
		public ConcurrentDictionary<string, IBusinessRepository> BusinessRepositories { get; } = new ConcurrentDictionary<string, IBusinessRepository>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Register & Update settings
		internal static void Register(Type type)
		{
			// check
			if (type == null || RepositoryMediator.RepositoryDefinitions.ContainsKey(type))
				return;

			// get info of the definition
			var definitionInfo = type.GetCustomAttribute<RepositoryAttribute>(false);
			if (definitionInfo == null)
				return;

			// initialize
			var definition = new RepositoryDefinition
			{
				Type = type,
				ServiceName = !string.IsNullOrWhiteSpace(definitionInfo.ServiceName) ? definitionInfo.ServiceName : "",
				ID = !string.IsNullOrWhiteSpace(definitionInfo.ID) ? definitionInfo.ID : "",
				Directory = !string.IsNullOrWhiteSpace(definitionInfo.Directory) ? definitionInfo.Directory : null,
				Title = !string.IsNullOrWhiteSpace(definitionInfo.Title) ? definitionInfo.Title : "",
				Description = !string.IsNullOrWhiteSpace(definitionInfo.Description) ? definitionInfo.Description : "",
				Icon = !string.IsNullOrWhiteSpace(definitionInfo.Icon) ? definitionInfo.Icon : null,
				ExtendedPropertiesTableName = !string.IsNullOrWhiteSpace(definitionInfo.ExtendedPropertiesTableName) ? definitionInfo.ExtendedPropertiesTableName : "T_Data_Extended_Properties"
			};

			// update into collection
			if (RepositoryMediator.RepositoryDefinitions.TryAdd(type, definition) && RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"The repository definition was registered [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]");
		}

		internal static void Update(JObject settings, Action<string, Exception> tracker = null)
		{
			// check
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// prepare
			Type aliasOf = null;
			var isAlias = settings["isAlias"] != null
				? "true".IsEquals((settings["isAlias"] as JValue).Value as string)
				: false;
			if (isAlias)
			{
				var targetType = settings["target"] != null
					? (settings["target"] as JValue).Value as string
					: null;

				if (string.IsNullOrWhiteSpace(targetType))
					isAlias = false;
				else
				{
					aliasOf = Type.GetType(targetType);
					if (aliasOf == null)
						isAlias = false;
				}
			}

			var type = Type.GetType((settings["type"] as JValue).Value as string);
			if (type == null)
				return;

			// clone definition for new alias
			if (isAlias)
			{
				if (!RepositoryMediator.RepositoryDefinitions.TryGetValue(aliasOf, out var targetDef))
					throw new InformationInvalidException($"The target type named '{aliasOf.GetTypeName()}' is not available");
				RepositoryMediator.RepositoryDefinitions.Add(type, RepositoryMediator.RepositoryDefinitions[aliasOf].Clone(cloneDef =>
				{
					cloneDef.IsAlias = true;
					cloneDef.Extras = targetDef.Extras.Clone();
				}));
			}

			// get definition
			var definition = RepositoryMediator.GetRepositoryDefinition(type);
			if (definition == null)
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			if (string.IsNullOrEmpty(data))
				throw new ArgumentNullException("primaryDataSource", "[primaryDataSource] attribute of settings");
			else if (!RepositoryMediator.DataSources.ContainsKey(data))
				throw new InformationInvalidException($"The data source named '{data}' is not available");
			definition.PrimaryDataSourceName = data;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			definition.SecondaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			definition.SyncDataSourceNames = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;

			data = settings["versionDataSource"] != null
				? (settings["versionDataSource"] as JValue).Value as string
				: null;
			definition.VersionDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["trashDataSource"] != null
				? (settings["trashDataSource"] as JValue).Value as string
				: null;
			definition.TrashDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			definition.AutoSync = "true".IsEquals(settings["autoSync"] != null ? (settings["autoSync"] as JValue).Value as string : "false");

			tracker?.Invoke(
				$"Settings of a repository was updated [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]" + "\r\n" +
				$"- Primary data source: {(definition.PrimaryDataSource != null ? $"{definition.PrimaryDataSource.Name} ({definition.PrimaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Secondary data source: {(definition.SecondaryDataSource != null ? $"{definition.SecondaryDataSource.Name} ({definition.SecondaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Sync data sources: {(definition.SyncDataSources.Count > 0 ? definition.SyncDataSources.Select(dataSource => $"{dataSource.Name} ({dataSource.Mode})").ToString(", ") : "None")}" + "\r\n" +
				$"- Version data source: {definition.VersionDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Trash data source: {definition.TrashDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Auto sync: {definition.AutoSync}"
			, null);
		}
		#endregion

		#region Register/Unregister business repositories
		/// <summary>
		/// Registers a business repository (means a business module at run-time)
		/// </summary>
		/// <param name="businessRepository"></param>
		public bool Register(IBusinessRepository businessRepository)
		{
			if (businessRepository != null && !string.IsNullOrWhiteSpace(businessRepository.ID))
			{
				this.BusinessRepositories[businessRepository.ID] = businessRepository;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Unregisters a business repository (means a business module at run-time)
		/// </summary>
		/// <param name="businessRepositoryID"></param>
		public bool Unregister(string businessRepositoryID)
		{
			if (!string.IsNullOrWhiteSpace(businessRepositoryID))
			{
				this.BusinessRepositories.Remove(businessRepositoryID);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Unregisters a business repository (means a business module at run-time)
		/// </summary>
		/// <param name="businessRepository"></param>
		public bool Unregister(IBusinessRepository businessRepository)
			=> this.Unregister(businessRepository?.ID);
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a repository entity definition (means a content-type definition)
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Type.FullName}")]
	public class EntityDefinition
	{
		public EntityDefinition() { }

		#region Properties
		/// <summary>
		/// Gets the type of the class that responsibility to process data of the repository entity
		/// </summary>
		public Type Type { get; internal set; }

		/// <summary>
		/// Gets the name of the primary data source
		/// </summary>
		public string PrimaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the primary data-source
		/// </summary>
		public DataSource PrimaryDataSource => RepositoryMediator.GetDataSource(this.PrimaryDataSourceName);

		/// <summary>
		/// Gets the name of the secondary data source
		/// </summary>
		public string SecondaryDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the secondary data-source
		/// </summary>
		public DataSource SecondaryDataSource => RepositoryMediator.GetDataSource(this.SecondaryDataSourceName);

		/// <summary>
		/// Gets the names of the all data-sources that available for sync
		/// </summary>
		public string SyncDataSourceNames { get; internal set; }

		/// <summary>
		/// Gets the other data sources that are available for synchronizing
		/// </summary>
		public List<DataSource> SyncDataSources => !string.IsNullOrWhiteSpace(this.SyncDataSourceNames)
			? this.SyncDataSourceNames.ToList()
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(name => RepositoryMediator.GetDataSource(name))
				.Where(dataSource => dataSource != null)
				.ToList()
			: null;

		/// <summary>
		/// Gets the name of the data source for storing information of versioning contents
		/// </summary>
		public string VersionDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the data-source that use to store versioning contents
		/// </summary>
		public DataSource VersionDataSource => RepositoryMediator.GetDataSource(this.VersionDataSourceName);

		/// <summary>
		/// Gets the name of the data source for storing information of trash contents
		/// </summary>
		public string TrashDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the data-source that use to store trash contents
		/// </summary>
		public DataSource TrashDataSource => RepositoryMediator.GetDataSource(this.TrashDataSourceName);

		/// <summary>
		/// Gets or sets the name of the table in SQL database
		/// </summary>
		public string TableName { get; internal set; }

		/// <summary>
		/// Gets or sets the name of the collection in NoSQL database
		/// </summary>
		public string CollectionName { get; internal set; }

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to search using full-text method
		/// </summary>
		public bool Searchable { get; internal set; } = true;

		/// <summary>
		/// Gets the state to create new version when a repository entity object is updated
		/// </summary>
		public bool CreateNewVersionWhenUpdated { get; internal set; } = true;

		/// <summary>
		/// Gets that state that specified data of this repository entity is sync automatically between data sources
		/// </summary>
		public bool AutoSync { get; internal set; } = false;

		/// <summary>
		/// Gets the extra information of the repository entity definition
		/// </summary>
		public Dictionary<string, object> Extras { get; internal set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Gets the collection of all available attributes (properties and fields)
		/// </summary>
		public List<AttributeInfo> Attributes { get; } = new List<AttributeInfo>();

		/// <summary>
		/// Gets the collection of all available attributes (properties and fields) for working with forms
		/// </summary>
		public List<AttributeInfo> FormAttributes { get; } = new List<AttributeInfo>();

		/// <summary>
		/// Gets the name of primary key property
		/// </summary>
		internal string PrimaryKey { get; set; }

		/// <summary>
		/// Gets the information of the primary key
		/// </summary>
		public AttributeInfo PrimaryKeyInfo => this.Attributes.FirstOrDefault(attribute => attribute.Name.Equals(this.PrimaryKey));

		/// <summary>
		/// Gets the collection of sortable properties
		/// </summary>
		public List<string> SortableAttributes { get; } = new List<string> { "ID" };

		/// <summary>
		/// Gets the caching object for processing with caching data of this entity
		/// </summary>
		public ICache Cache { get; internal set; }

		/// <summary>
		/// Gets the type of the type that presents the repository definition of this repository entity definition
		/// </summary>
		public Type RepositoryDefinitionType { get; internal set; }

		/// <summary>
		/// Gets the repository definition of this repository entity definition
		/// </summary>
		public RepositoryDefinition RepositoryDefinition => this.RepositoryDefinitionType?.GetRepositoryDefinition();
		#endregion

		#region Properties [Content-Type Definition]
		/// <summary>
		/// Gets the name of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectName { get; internal set; }

		/// <summary>
		/// Gets the name prefix of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectNamePrefix { get; internal set; }

		/// <summary>
		/// Gets the name suffix of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectNameSuffix { get; internal set; }

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
		/// Gets or sets the name of the icon for working with user interfaces (when this object is defined as a content-type definition)
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// Gets the state that allow to use multiple instances, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleIntances { get; internal set; } = false;

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to index with global search module, default is true (when this object is defined as a content-type definition)
		/// </summary>
		public bool Indexable { get; internal set; } = false;

		/// <summary>
		/// Gets the state that allow to extend this entity by extended properties, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Extendable { get; internal set; } = false;

		/// <summary>
		/// Gets the type of parent entity definition (when this object is defined as a content-type definition)
		/// </summary>
		public Type ParentType => this.GetParentMappingAttribute()?.GetCustomAttribute<ParentMappingAttribute>()?.Type;

		/// <summary>
		/// Gets the collection of business repository entities (means business conten-types at run-time)
		/// </summary>
		public ConcurrentDictionary<string, IBusinessRepositoryEntity> BusinessRepositoryEntities { get; } = new ConcurrentDictionary<string, IBusinessRepositoryEntity>(StringComparer.OrdinalIgnoreCase);
		#endregion

		#region Register & Update settings
		internal static void Register(Type type)
		{
			// check
			if (type == null || RepositoryMediator.EntityDefinitions.ContainsKey(type))
				return;

			// get info of the definition
			var definitionInfo = type.GetCustomAttribute<EntityAttribute>(false);
			if (definitionInfo == null)
				return;

			// verify the name of table/collection
			if (string.IsNullOrWhiteSpace(definitionInfo.TableName) && string.IsNullOrWhiteSpace(definitionInfo.CollectionName))
				throw new InformationRequiredException($"The type [{type}] must have name of SQL table or NoSQL collection");

			// verify the mappings
			var properties = type.GetPublicAttributes();

			foreach (var attribute in properties.Where(attribute => attribute.IsMappings()))
			{
				if (!attribute.IsGenericListOrHashSet() || !attribute.GetGenericTypeArguments().First().Equals(typeof(string)))
					throw new InformationInvalidException($"The attribute [{attribute.Name}] must be list or hash-set of string for using as mapping");
			}

			var parentMappings = properties.Where(attribute => attribute.IsParentMapping()).ToList();
			if (parentMappings.Count > 0)
			{
				if (parentMappings.Count > 1)
					throw new InformationInvalidException($"The type [{type}] got multiple mappings with parent entity definition");
				else if (parentMappings.First().Type == null)
					throw new InformationRequiredException($"The attribute [{parentMappings.First().Name}] must have the type of parent entity definition");
			}
			else if (properties.Count(attribute => attribute.IsMultipleParentMappings()) > 0)
				throw new InformationInvalidException($"The type [{type}] got multiple parent mappings but got no information of the parent entity definition");

			// verify alias
			var aliasProperties = properties.Where(attribute => attribute.IsAlias()).ToList();
			if (aliasProperties.Count > 0)
			{
				if (aliasProperties.Count > 1)
					throw new InformationInvalidException($"The type [{type}] got multiple alias properties");
				else if (!typeof(IAliasEntity).IsAssignableFrom(type))
					throw new InformationRequiredException($"The type [{type}] got alias property but not implement the 'IAliasEntity' interface");
				var aliasInfo = aliasProperties.First().GetCustomAttribute<AliasAttribute>();
				if (!string.IsNullOrWhiteSpace(aliasInfo.Properties))
				{
					var aliasProps = aliasInfo.Properties.ToHashSet(",", true);
					var missingProps = aliasProps.Except(properties.Where(attribute => aliasProps.Contains(attribute.Name)).Select(attribute => attribute.Name), StringComparer.OrdinalIgnoreCase).ToList();
					if (missingProps.Count > 0)
						throw new InformationInvalidException($"The properties to make the alias combination of the type [{type}] are invalid [missing: {missingProps.Join(", ")}]");
				}
			}

			// initialize
			var definition = new EntityDefinition
			{
				Type = type,
				TableName = definitionInfo.TableName,
				CollectionName = definitionInfo.CollectionName,
				Searchable = definitionInfo.Searchable,
				CreateNewVersionWhenUpdated = definitionInfo.CreateNewVersionWhenUpdated,
				ObjectName = !string.IsNullOrWhiteSpace(definitionInfo.ObjectName) ? definitionInfo.ObjectName : type.GetTypeName(true),
				ObjectNamePrefix = !string.IsNullOrWhiteSpace(definitionInfo.ObjectNamePrefix) ? definitionInfo.ObjectNamePrefix : null,
				ObjectNameSuffix = !string.IsNullOrWhiteSpace(definitionInfo.ObjectNameSuffix) ? definitionInfo.ObjectNameSuffix : null,
				ID = !string.IsNullOrWhiteSpace(definitionInfo.ID) ? definitionInfo.ID : "",
				Title = !string.IsNullOrWhiteSpace(definitionInfo.Title) ? definitionInfo.Title : "",
				Description = !string.IsNullOrWhiteSpace(definitionInfo.Description) ? definitionInfo.Description : "",
				Icon = !string.IsNullOrWhiteSpace(definitionInfo.Icon) ? definitionInfo.Icon : null,
				MultipleIntances = definitionInfo.MultipleIntances,
				Indexable = definitionInfo.Indexable,
				Extendable = definitionInfo.Extendable
			};

			// public properties
			var numberOfPrimaryKeys = 0;
			foreach (var property in properties)
			{
				// by-pass if ignore and not defined as a form control
				if (property.IsIgnored() && !property.IsFormControl())
					continue;

				// create
				var attribute = new AttributeInfo(property);

				// primary key
				if (!property.IsIgnored() && attribute.GetCustomAttribute<PrimaryKeyAttribute>() is PrimaryKeyAttribute primaryKeyInfo)
				{
					attribute.Column = primaryKeyInfo.Column;
					attribute.NotNull = true;
					attribute.NotEmpty = true;
					if (primaryKeyInfo.MaxLength > 0)
						attribute.MaxLength = primaryKeyInfo.MaxLength;

					definition.PrimaryKey = attribute.Name;
					numberOfPrimaryKeys += 1;
				}

				// property
				if (attribute.GetCustomAttribute<PropertyAttribute>() is PropertyAttribute propertyInfo)
				{
					attribute.Column = propertyInfo.Column;
					attribute.NotNull = propertyInfo.NotNull;
					if (attribute.IsStringType())
					{
						if (propertyInfo.NotEmpty)
							attribute.NotEmpty = true;
						if (propertyInfo.IsCLOB)
							attribute.IsCLOB = true;
						if (propertyInfo.MinLength > 0)
							attribute.MinLength = propertyInfo.MinLength;
						if (!propertyInfo.IsCLOB)
							attribute.MaxLength = propertyInfo.MaxLength > 0 && propertyInfo.MaxLength < 4000
								? propertyInfo.MaxLength
								: attribute.IsParentMapping() || attribute.Name.EndsWith("ID") ? 32 : 4000;
					}
					else
					{
						if (!string.IsNullOrWhiteSpace(propertyInfo.MinValue))
							attribute.MinValue = propertyInfo.MinValue;
						if (!string.IsNullOrWhiteSpace(propertyInfo.MaxValue))
							attribute.MaxValue = propertyInfo.MaxValue;
					}
				}

				// update definitions
				definition.FormAttributes.Add(attribute);
				if (!property.IsIgnored())
				{
					definition.Attributes.Add(attribute);
					if (attribute.IsSortable())
						definition.SortableAttributes.Add(attribute.Name);
				}
			};

			// check primary key
			if (numberOfPrimaryKeys < 1)
				throw new InformationRequiredException($"The type [{type}] got no primary-key");
			else if (numberOfPrimaryKeys > 1)
				throw new InformationInvalidException($"The type [{type}] got multiple primary-keys");

			// private attributes
			type.GetPrivateAttributes().Where(field => !field.IsIgnored()).ForEach(field =>
			{
				// create
				var attribute = new AttributeInfo(field);

				// update info
				if (attribute.GetCustomAttribute<FieldAttribute>() is FieldAttribute fieldInfo)
				{
					attribute.Column = fieldInfo.Column;
					attribute.NotNull = fieldInfo.NotNull;
					if (attribute.IsStringType())
					{
						if (fieldInfo.NotEmpty)
							attribute.NotEmpty = true;
						if (fieldInfo.IsCLOB)
							attribute.IsCLOB = true;
						if (fieldInfo.MinLength > 0)
							attribute.MinLength = fieldInfo.MinLength;
						if (!fieldInfo.IsCLOB)
							attribute.MaxLength = fieldInfo.MaxLength > 0 && fieldInfo.MaxLength < 4000
								? fieldInfo.MaxLength
								: attribute.IsParentMapping() || attribute.Name.EndsWith("ID") ? 32 : 4000;
					}
					else
					{
						if (!string.IsNullOrWhiteSpace(fieldInfo.MinValue))
							attribute.MinValue = fieldInfo.MinValue;
						if (!string.IsNullOrWhiteSpace(fieldInfo.MaxValue))
							attribute.MaxValue = fieldInfo.MaxValue;
					}

					// update definitions
					definition.Attributes.Add(attribute);
				}
			});

			// cache
			if (definitionInfo.CacheClass != null && !string.IsNullOrWhiteSpace(definitionInfo.CacheName))
			{
				var cache = definitionInfo.CacheClass.GetStaticObject(definitionInfo.CacheName);
				definition.Cache = cache != null && cache is ICache ? cache as ICache : null;
			}

			// type of repository definition
			var rootType = typeof(object);
			var baseType = typeof(RepositoryBase);
			var parentType = type.BaseType;
			var grandParentType = parentType.BaseType;
			while (grandParentType.BaseType != null && grandParentType.BaseType != rootType && grandParentType.BaseType != baseType)
			{
				parentType = parentType.BaseType;
				grandParentType = parentType.BaseType;
			}
			var repositoryDefinitionTypeName = parentType.GetTypeName();
			definition.RepositoryDefinitionType = Type.GetType(repositoryDefinitionTypeName.Left(repositoryDefinitionTypeName.IndexOf("[")) + repositoryDefinitionTypeName.Substring(repositoryDefinitionTypeName.IndexOf("]") + 2));

			// update into collection
			if (RepositoryMediator.EntityDefinitions.TryAdd(type, definition) && RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(
					$"The repository entity definition was registered [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]" + "\r\n" +
					$"- Attributes: " + "\r\n" + definition.Attributes.ToJArray(attribute => new JObject
					{
						{ "Name", attribute.Name },
						{ "Type", attribute.Type.GetTypeName(true) },
						{ "MinLength", attribute.MinLength },
						{ "MaxLength", attribute.MaxLength },
						{ "MinValue", attribute.MinValue },
						{ "MaxValue", attribute.MaxValue },
						{ "NotNull", attribute.NotNull },
						{ "NotEmpty", attribute.NotEmpty },
						{ "CLOB", attribute.IsCLOB }
					}).ToString(Newtonsoft.Json.Formatting.Indented)
				);
		}

		internal static void Update(JObject settings, Action<string, Exception> tracker = null)
		{
			// check
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["type"] == null)
				throw new ArgumentNullException("type", "[type] attribute of settings");

			// prepare
			var definition = RepositoryMediator.GetEntityDefinition(Type.GetType((settings["type"] as JValue).Value as string));
			if (definition == null)
				return;

			// update
			var data = settings["primaryDataSource"] != null
				? (settings["primaryDataSource"] as JValue).Value as string
				: null;
			definition.PrimaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["secondaryDataSource"] != null
				? (settings["secondaryDataSource"] as JValue).Value as string
				: null;
			definition.SecondaryDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			definition.SyncDataSourceNames = settings["syncDataSources"] != null
				? (settings["syncDataSources"] as JValue).Value as string
				: null;

			data = settings["versionDataSource"] != null
				? (settings["versionDataSource"] as JValue).Value as string
				: null;
			definition.VersionDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			data = settings["trashDataSource"] != null
				? (settings["trashDataSource"] as JValue).Value as string
				: null;
			definition.TrashDataSourceName = !string.IsNullOrEmpty(data) && RepositoryMediator.DataSources.ContainsKey(data)
				? data
				: null;

			// individual caching storage
			if (settings["cacheRegion"] != null)
			{
				var cacheRegion = (settings["cacheRegion"] as JValue).Value as string;
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
				var cacheProvider = settings["cacheProvider"] != null
					? (settings["cacheProvider"] as JValue).Value as string
					: null;
				definition.Cache = new Cache(cacheRegion, cacheExpirationTime, cacheActiveSynchronize, cacheProvider);
			}

			definition.AutoSync = settings["autoSync"] != null
				? "true".IsEquals((settings["autoSync"] as JValue).Value as string)
				: definition.RepositoryDefinition.AutoSync;

			tracker?.Invoke(
				$"Settings of a repository entity was updated [{definition.Type.GetTypeName()}{(string.IsNullOrWhiteSpace(definition.Title) ? "" : $" => {definition.Title}")}]" + "\r\n" +
				$"- Primary data source: {(definition.PrimaryDataSource != null ? $"{definition.PrimaryDataSource.Name} ({definition.PrimaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Secondary data source: {(definition.SecondaryDataSource != null ? $"{definition.SecondaryDataSource.Name} ({definition.SecondaryDataSource.Mode})" : "None")}" + "\r\n" +
				$"- Sync data sources: {(definition.SyncDataSources != null && definition.SyncDataSources.Count > 0 ? definition.SyncDataSources.Select(dataSource => $"{dataSource.Name} ({dataSource.Mode})").ToString(", ") : "None")}" + "\r\n" +
				$"- Version data source: {definition.VersionDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Trash data source: {definition.TrashDataSource?.Name ?? "(None)"}" + "\r\n" +
				$"- Auto sync: {definition.AutoSync}"
			, null);
		}

		/// <summary>
		/// Sets the cache storage of a repository entity definition
		/// </summary>
		/// <param name="type">The type that presents information of a repository entity definition</param>
		/// <param name="cache">The cache storage</param>
		public void SetCacheStorage(Type type, Cache cache)
		{
			if (type != null && RepositoryMediator.EntityDefinitions.ContainsKey(type))
				RepositoryMediator.EntityDefinitions[type].Cache = cache;
		}
		#endregion

		#region Register/Unregister business repository entities
		/// <summary>
		/// Registers a business repository entity (means a business content-type at run-time)
		/// </summary>
		/// <param name="businessRepositoryEntity"></param>
		public bool Register(IBusinessRepositoryEntity businessRepositoryEntity)
		{
			if (businessRepositoryEntity != null && !string.IsNullOrWhiteSpace(businessRepositoryEntity.ID))
			{
				this.BusinessRepositoryEntities[businessRepositoryEntity.ID] = businessRepositoryEntity;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Unregisters a business repository entity (means a business content-type at run-time)
		/// </summary>
		/// <param name="businessRepositoryEntityID"></param>
		public bool Unregister(string businessRepositoryEntityID)
		{
			if (!string.IsNullOrWhiteSpace(businessRepositoryEntityID))
			{
				this.BusinessRepositoryEntities.Remove(businessRepositoryEntityID);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Unregisters a business repository entity (means a business content-type at run-time)
		/// </summary>
		/// <param name="businessRepositoryEntity"></param>
		public bool Unregister(IBusinessRepositoryEntity businessRepositoryEntity)
			=> this.Unregister(businessRepositoryEntity?.ID);
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents information of an attribute of a repository entity
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}")]
	public class AttributeInfo : ObjectService.AttributeInfo
	{
		public AttributeInfo() : this(null) { }

		public AttributeInfo(ObjectService.AttributeInfo derived) : base(derived?.Name, derived?.Info) { }

		public string Column { get; internal set; }

		public bool NotNull { get; internal set; } = false;

		public bool? NotEmpty { get; internal set; }

		public bool? IsCLOB { get; internal set; }

		public int? MinLength { get; internal set; }

		public int? MaxLength { get; internal set; }

		public string MinValue { get; internal set; }

		public string MaxValue { get; internal set; }
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a definition of an extended property of an entiry in a respository 
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
	public sealed class ExtendedPropertyDefinition
	{
		public ExtendedPropertyDefinition(JObject json = null)
			=> this.CopyFrom(json ?? new JObject());

		#region Properties
		/// <summary>
		/// Gets or sets the name
		/// </summary>
		public string Name { get; set; } = "";

		/// <summary>
		/// Gets or sets the mode
		/// </summary>
		public ExtendedPropertyMode Mode { get; set; } = ExtendedPropertyMode.SmallText;

		/// <summary>
		/// Gets or sets the name of column for storing data (when repository mode is SQL)
		/// </summary>
		public string Column { get; set; } = "";

		/// <summary>
		/// Gets or sets the default value
		/// </summary>
		public string DefaultValue { get; set; } = "";

		/// <summary>
		/// Gets or sets the formula for computing default value
		/// </summary>
		public string DefaultValueFormula { get; set; } = "";
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
					case ExtendedPropertyMode.Number:
						return typeof(int);

					case ExtendedPropertyMode.Decimal:
						return typeof(decimal);

					case ExtendedPropertyMode.DateTime:
						return typeof(DateTime);
				}
				return typeof(string);
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
					case ExtendedPropertyMode.Number:
						return typeof(int).GetDbType();

					case ExtendedPropertyMode.Decimal:
						return typeof(decimal).GetDbType();

					case ExtendedPropertyMode.DateTime:
						return DbType.AnsiString;
				}
				return typeof(string).GetDbType();
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
		internal static HashSet<string> ReservedWords { get; } = "ExtendedProperties,Add,External,Procedure,All,Fetch,Public,Alter,File,RaisError,And,FillFactor,Read,Any,For,ReadText,As,Foreign,ReConfigure,Asc,FreeText,References,Authorization,FreeTextTable,Replication,Backup,From,Restore,Begin,Full,Restrict,Between,Function,Return,Break,Goto,Revert,Browse,Grant,Revoke,Bulk,Group,Right,By,Having,Rollback,Cascade,Holdlock,Rowcount,Case,RowGuidCol,Check,Identity,Insert,Rule,Checkpoint,Identitycol,Save,Close,If,Schema,Clustered,In,SecurityAudit,Coalesce,Index,Select,Collate,Inner,SemanticKeyPhraseTable,Column,SemanticSimilarityDetailsTable,Commit,Intersect,SemanticSimilarityTable,Compute,Into,Session,User,Constraint,Is,Set,Contains,Join,Setuser,ContainsTable,Key,Shutdown,Continue,Kill,Some,Convert,Left,Statistics,Create,Like,System,Cross,Lineno,Table,Current,Load,TableSample,Current_Date,Current_Time,Current_Timestamp,Merge,TextSize,National,Then,NoCheck,To,Current_User,NonClustered,Top,Cursor,Not,Tran,Database,Null,Transaction,Dbcc,NullIf,Trigger,Deallocate,Of,Truncate,Declare,Off,Try_Convert,Default,Offsets,Tsequal,Delete,On,Union,Deny,Open,Unique,Desc,OpenDataSource,Unpivot,Disk,Openquery,Update,Distinct,OpenRowset,UpdateText,Distributed,OpenXml,Use,Double,Option,User,Drop,Or,Values,Dump,Order,Varying,Else,Outer,View,End,Over,Waitfor,Errlvl,Percent,When,Escape,Pivot,Where,Except,Plan,While,Exec,Precision,With,Execute,Primary,Exists,Print,WriteText,Exit,Proc".ToLower().ToHashSet();

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
		public object GetDefaultValue() => null;

		public override string ToString() => this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a data source
	/// </summary>
	[Serializable, DebuggerDisplay("Name = {Name}, Mode = {Mode}")]
	public class DataSource
	{
		public DataSource() { }

		#region Properties
		/// <summary>
		/// Gets the name of the data source
		/// </summary>
		public string Name { get; internal set; }

		/// <summary>
		/// Gets the working mode
		/// </summary>
		public RepositoryMode Mode { get; internal set; }

		/// <summary>
		/// Gets the name of the connection string (for working with database server)
		/// </summary>
		public string ConnectionStringName { get; internal set; }

		/// <summary>
		/// Gets or sets the connection string (for working with database server)
		/// </summary>
		public string ConnectionString { get; set; }

		/// <summary>
		/// Gets the name of the database (for working with database server)
		/// </summary>
		public string DatabaseName { get; internal set; }
		#endregion

		#region Helper methods
		internal static DataSource FromJson(JObject settings)
		{
			if (settings == null)
				throw new ArgumentNullException("settings");
			else if (settings["name"] == null)
				throw new ArgumentNullException("name", "[name] attribute of settings");
			else if (settings["mode"] == null)
				throw new ArgumentNullException("mode", "[mode] attribute of settings");

			// initialize
			var dataSource = new DataSource()
			{
				Name = (settings["name"] as JValue).Value as string,
				Mode = ((settings["mode"] as JValue).Value as string).ToEnum<RepositoryMode>()
			};

			// name of connection string
			if (settings["connectionStringName"] == null)
				throw new ArgumentNullException("connectionStringName", "[connectionStringName] attribute of settings");
			dataSource.ConnectionStringName = (settings["connectionStringName"] as JValue).Value as string;

			// connection string
			dataSource.ConnectionString = settings["connectionString"] != null
				? (settings["connectionString"] as JValue).Value as string
				: null;

			// name of database
			if (settings["databaseName"] != null)
				dataSource.DatabaseName = (settings["databaseName"] as JValue).Value as string;
			else if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
				throw new ArgumentNullException("databaseName", "[databaseName] attribute of settings");

			return dataSource;
		}
		#endregion

	}
}