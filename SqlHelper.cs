#region Related components
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

using System.Data;
using System.Data.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Caching;
using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Collection of methods for working with SQL database (like SQL Server, MySQL, Oracle, ...)
	/// </summary>
	public static class SqlHelper
	{

		#region Provider Factory
		/// <summary>
		/// Gets the database provider factory for working with SQL database
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <returns></returns>
		public static DbProviderFactory GetProviderFactory(DataSource dataSource)
		{
			var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? RepositoryMediator.GetConnectionStringSettings(dataSource)
				: null;
			return connectionStringSettings != null && !string.IsNullOrEmpty(connectionStringSettings.ProviderName)
				? DbProviderFactories.GetFactory(connectionStringSettings.ProviderName)
				: null;
		}
		#endregion

		#region Connection
		/// <summary>
		/// Gets the connection for working with SQL database
		/// </summary>
		/// <param name="existingConnection">The object that presents a database connection to copy settings from</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DbConnection existingConnection)
		{
			if (existingConnection == null)
				return null;
			var connection = existingConnection.GetType().CreateInstance() as DbConnection;
			connection.ConnectionString = existingConnection.ConnectionString;
			return connection;
		}

		/// <summary>
		/// Gets the connection for working with SQL database
		/// </summary>
		/// <param name="providerFactory">The object that presents information of a database provider factory</param>
		/// <param name="connectionString">The string that presents information to connect with SQL database</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DbProviderFactory providerFactory, string connectionString)
		{
			if (providerFactory == null)
				return null;
			var connection = providerFactory.CreateConnection();
			if (!string.IsNullOrWhiteSpace(connectionString))
				connection.ConnectionString = connectionString;
			return connection;
		}

		/// <summary>
		/// Gets the connection for working with SQL database
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <param name="providerFactory">The object that presents information of a database provider factory</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DataSource dataSource, DbProviderFactory providerFactory = null)
		{		
			var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? RepositoryMediator.GetConnectionStringSettings(dataSource)
				: null;
			return SqlHelper.GetConnection(providerFactory != null ? providerFactory : SqlHelper.GetProviderFactory(dataSource), connectionStringSettings != null ? connectionStringSettings.ConnectionString : null);
		}
		#endregion

		#region Command
		static DbCommand CreateCommand(this Tuple<string, List<DbParameter>> info, DbConnection connection)
		{
			var command = connection.CreateCommand();
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter =>
			{
				command.Parameters.Add(parameter);
			});
			return command;
		}

		static DbCommand CreateCommand(this Tuple<string, List<DbParameter>> info, DbProviderFactory providerFactory)
		{
			var command = providerFactory.CreateCommand();
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter =>
			{
				command.Parameters.Add(parameter);
			});
			return command;
		}

		static DbParameter CreateParameter(this KeyValuePair<string, object> info, DbProviderFactory providerFactory)
		{
			var parameter = providerFactory.CreateParameter();
			parameter.ParameterName = info.Key;
			parameter.Value = info.Value;
			parameter.DbType = info.Key.EndsWith("ID")
				? DbType.AnsiStringFixedLength
				:  SqlHelper.DbTypes[info.Value.GetType()];
			return parameter;
		}
		#endregion

		#region DbTypes
		internal static Dictionary<Type, DbType> DbTypes = new Dictionary<Type, DbType>()
		{
			{ typeof(String), DbType.String },
			{ typeof(Byte[]), DbType.Binary },
			{ typeof(Byte), DbType.Byte },
			{ typeof(SByte), DbType.SByte },
			{ typeof(Int16), DbType.Int16 },
			{ typeof(UInt16), DbType.UInt16 },
			{ typeof(Int32), DbType.Int32 },
			{ typeof(UInt32), DbType.UInt32 },
			{ typeof(Int64), DbType.Int64 },
			{ typeof(UInt64), DbType.UInt64 },
			{ typeof(Single), DbType.Single },
			{ typeof(Double), DbType.Double },
			{ typeof(Decimal), DbType.Decimal },
			{ typeof(Boolean), DbType.Boolean },
			{ typeof(Char), DbType.StringFixedLength },
			{ typeof(Guid), DbType.Guid },
			{ typeof(DateTime), DbType.DateTime },
			{ typeof(DateTimeOffset), DbType.DateTimeOffset },
			{ typeof(Byte?), DbType.Byte },
			{ typeof(SByte?), DbType.SByte },
			{ typeof(Int16?), DbType.Int16 },
			{ typeof(UInt16?), DbType.UInt16 },
			{ typeof(Int32?), DbType.Int32 },
			{ typeof(UInt32?), DbType.UInt32 },
			{ typeof(Int64?), DbType.Int64 },
			{ typeof(UInt64?), DbType.UInt64 },
			{ typeof(Single?), DbType.Single },
			{ typeof(Double?), DbType.Double },
			{ typeof(Decimal?), DbType.Decimal },
			{ typeof(Boolean?), DbType.Boolean },
			{ typeof(Char?), DbType.StringFixedLength },
			{ typeof(Guid?), DbType.Guid },
			{ typeof(DateTime?), DbType.DateTime },
			{ typeof(DateTimeOffset?), DbType.DateTimeOffset },
		};

		internal static DbType GetDbType(this ObjectService.AttributeInfo attribute)
		{
			return attribute.IsStoredAsString()
				? DbType.AnsiStringFixedLength
				: attribute.Type.IsStringType() && (attribute.Name.EndsWith("ID") || attribute.MaxLength.Equals(32))
					? DbType.AnsiStringFixedLength
					: SqlHelper.DbTypes[attribute.Type];
		}
		#endregion

		#region Create
		static Tuple<string, List<DbParameter>> PrepareCreateOrigin<T>(this T @object, DbProviderFactory providerFactory) where T : class
		{
			string columns = "", values = "";
			var parameters = new List<DbParameter>();
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach(var attribute in definition.Attributes)
			{
				var value = @object.GetAttributeValue(attribute.Name);
				if (value == null && attribute.IsIgnoredIfNull())
					continue;

				columns += (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column) + ",";
				values += "@" + attribute.Name + ",";

				var parameter = providerFactory.CreateParameter();
				parameter.ParameterName = "@" + attribute.Name;
				if (attribute.IsStoredAsJson())
				{
					parameter.DbType = DbType.String;
					parameter.Value = value.ToJson().ToString(Newtonsoft.Json.Formatting.None);
				}
				else
				{
					parameter.DbType = attribute.GetDbType();
					parameter.Value = attribute.IsStoredAsString()
						? ((DateTime)value).ToDTString()
						: value;
				}
				parameters.Add(parameter);
			}

			return new Tuple<string, List<DbParameter>>(
					"INSERT INTO " + definition.TableName + " (" + columns.Left(columns.Length - 1) + ") VALUES (" + values.Left(values.Length - 1) + ")", 
					parameters
				);
		}

		static Tuple<string, List<DbParameter>> PrepareCreateExtent<T>(this T @object, DbProviderFactory providerFactory) where T : class
		{
			var columns = "ID,SystemID,RepositoryID,EntityID";
			var values = "@ID,@SystemID,@RepositoryID,@EntityID";
			var parameters = new List<DbParameter>();

			var parameter = providerFactory.CreateParameter();
			parameter.ParameterName = "@ID";
			parameter.Value = (@object as IBusinessEntity).ID;
			parameter.DbType = DbType.StringFixedLength;
			parameters.Add(parameter);

			parameter = providerFactory.CreateParameter();
			parameter.ParameterName = "@SystemID";
			parameter.Value = (@object as IBusinessEntity).SystemID;
			parameter.DbType = DbType.StringFixedLength;
			parameters.Add(parameter);

			parameter = providerFactory.CreateParameter();
			parameter.ParameterName = "@RepositoryID";
			parameter.Value = (@object as IBusinessEntity).RepositoryID;
			parameter.DbType = DbType.StringFixedLength;
			parameters.Add(parameter);

			parameter = providerFactory.CreateParameter();
			parameter.ParameterName = "@EntityID";
			parameter.Value = (@object as IBusinessEntity).EntityID;
			parameter.DbType = DbType.StringFixedLength;
			parameters.Add(parameter);

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var attributes = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
			foreach (var attribute in attributes)
			{
				columns += attribute.Column + ",";
				values += "@" + attribute.Name + ",";

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name)
					? (@object as IBusinessEntity).ExtendedProperties[attribute.Name]
					: attribute.GetDefaultValue();

				parameter = providerFactory.CreateParameter();
				parameter.ParameterName = "@" + attribute.Name;
				parameter.DbType = attribute.DbType;
				parameter.Value = attribute.Type.Equals(typeof(DateTime))
					? ((DateTime)value).ToDTString()
					: value;
				parameters.Add(parameter);
			}

			return new Tuple<string, List<DbParameter>>(
					"INSERT INTO " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " (" + columns.Left(columns.Length - 1) + ") VALUES (" + values.Left(values.Length - 1) + ")",
					parameters
				);
		}

		/// <summary>
		/// Creates new the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		public static void Create<T>(this RepositoryContext context, DataSource dataSource, T @object) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot create new because the object is null");

			var providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				connection.Open();

				var command = @object.PrepareCreateOrigin(providerFactory).CreateCommand(connection);
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareCreateExtent(providerFactory).CreateCommand(connection);
					command.ExecuteNonQuery();
				}
			}
		}

		/// <summary>
		/// Creates new the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task CreateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot create new because the object is null");

			var providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = @object.PrepareCreateOrigin(providerFactory).CreateCommand(connection);
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareCreateExtent(providerFactory).CreateCommand(connection);
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}
		#endregion

		#region Get
		static Tuple<string, List<DbParameter>> PrepareGetOrigin<T>(this T @object, string id, DbProviderFactory providerFactory) where T : class
		{
			var statement = "";
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach (var attribute in definition.Attributes)
			{
				if (attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) == null)
					continue;

				statement += "Origin." + (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column) + ",";
			};

			if (statement.Equals(""))
				return null;

			var info = Filters<T>.Equals(definition.PrimaryKey, id).GetSqlStatement();
			return new Tuple<string, List<DbParameter>>
			(
				"SELECT TOP 1 " + statement.Left(statement.Length - 1) + " FROM " + definition.TableName + " AS Origin WHERE " + info.Item1,
				new List<DbParameter>()
				{
					info.Item2.First().CreateParameter(providerFactory)
				}
			);
		}

		static Tuple<string, List<DbParameter>> PrepareGetExtent<T>(this T @object, string id, DbProviderFactory providerFactory, List<ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			var statement = "Origin.ID";
			foreach (var attribute in extendedProperties)
				statement += "Origin." + attribute.Column + " AS " + attribute.Name + ",";

			var info = Filters<T>.Equals("ID", id).GetSqlStatement();
			return new Tuple<string, List<DbParameter>>
			(
				"SELECT TOP 1 " + statement.Left(statement.Length - 1) + " FROM " + RepositoryMediator.GetEntityDefinition<T>().RepositoryDefinition.ExtendedPropertiesTableName + " AS Origin WHERE " + info.Item1,
				new List<DbParameter>()
				{
					info.Item2.First().CreateParameter(providerFactory)
				}
			);
		}

		static T Copy<T>(this T @object, DbDataReader reader, Dictionary<string, ObjectService.AttributeInfo> standardProperties) where T : class
		{
			@object = @object != null
				? @object
				: ObjectService.CreateInstance<T>();

			for (var index = 0; index < reader.FieldCount; index++)
			{
				var name = reader.GetName(index);
				if (!standardProperties.ContainsKey(name))
					continue;

				var attribute = standardProperties[name];
				var value = reader[index];
				if (value != null)
				{
					if (attribute.Type.IsDateTimeType() && attribute.IsStoredAsString())
						value = DateTime.Parse(value as string);
					else if (attribute.IsStoredAsJson())
					{
						var json = (value as string).StartsWith("[")
							? JArray.Parse(value as string) as JToken
							: JObject.Parse(value as string) as JToken;
						value = (new JsonSerializer()).Deserialize(new JTokenReader(json), attribute.Type);
					}
				}

				@object.SetAttributeValue(attribute, value, true);
			}

			return @object;
		}

		static T Copy<T>(this T @object, DbDataReader reader, Dictionary<string, ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			@object = @object != null
				? @object
				: ObjectService.CreateInstance<T>();

			if ((@object as IBusinessEntity).ExtendedProperties == null)
				(@object as IBusinessEntity).ExtendedProperties = new Dictionary<string, object>();

			for (var index = 0; index < reader.FieldCount; index++)
			{
				var name = reader.GetName(index);
				if (!extendedProperties.ContainsKey(name))
					continue;

				var attribute = extendedProperties[name];
				var value = reader[index];
				if (value != null && attribute.Type.IsDateTimeType())
					value = DateTime.Parse(value as string);

				if ((@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name))
					(@object as IBusinessEntity).ExtendedProperties[attribute.Name] = value.CastAs(attribute.Type);
				else
					(@object as IBusinessEntity).ExtendedProperties.Add(attribute.Name, value.CastAs(attribute.Type));
			}

			return @object;
		}

		/// <summary>
		/// Gets the record and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity</param>
		/// <returns></returns>
		public static T Get<T>(this RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			if (string.IsNullOrEmpty(id))
				return default(T);

			var @object = ObjectService.CreateInstance<T>();
			var providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				connection.Open();

				var command = @object.PrepareGetOrigin<T>(id, providerFactory).CreateCommand(connection);
				using (var reader = command.ExecuteReader())
				{
					if (reader.Read())
						@object = @object.Copy<T>(reader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name));
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = @object.PrepareGetExtent<T>(id, providerFactory, extendedProperties).CreateCommand(connection);
					using (var reader = command.ExecuteReader())
					{
						if (reader.Read())
							@object = @object.Copy<T>(reader, extendedProperties.ToDictionary(attribute => attribute.Name));
					}
				}
			}
			return @object;
		}

		/// <summary>
		/// Gets the record and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (string.IsNullOrEmpty(id))
				return default(T);

			var @object = ObjectService.CreateInstance<T>();
			var providerFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, providerFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = @object.PrepareGetOrigin<T>(id, providerFactory).CreateCommand(connection);
				using (var reader = await command.ExecuteReaderAsync(cancellationToken))
				{
					if (await reader.ReadAsync(cancellationToken))
						@object = @object.Copy<T>(reader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name));
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = @object.PrepareGetExtent<T>(id, providerFactory, extendedProperties).CreateCommand(connection);
					using (var reader = await command.ExecuteReaderAsync(cancellationToken))
					{
						if (await reader.ReadAsync(cancellationToken))
							@object = @object.Copy<T>(reader, extendedProperties.ToDictionary(attribute => attribute.Name));
					}
				}
			}
			return @object;
		}
		#endregion

		#region Get (first match)
		/// <summary>
		/// Finds the first record that matched with the filter and construc an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null) where T : class
		{
			return default(T);
		}

		/// <summary>
		/// Finds the first record that matched with the filter and construc an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<T>(default(T));
		}
		#endregion

		#region Replace
		/// <summary>
		/// Replaces the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		public static void Replace<T>(this RepositoryContext context, DataSource dataSource, T @object) where T : class
		{

		}

		/// <summary>
		/// Replaces the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}
		#endregion

		#region Update
		/// <summary>
		/// Updates the record of an object (only update changed atributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		public static void Update<T>(this RepositoryContext context, DataSource dataSource, T @object, List<string> attributes) where T : class
		{

		}

		/// <summary>
		/// Updates the record of an object (only update changed atributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}
		#endregion

		#region Delete
		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		public static void Delete<T>(this RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			
		}

		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object to delete</param>
		public static void Delete<T>(this RepositoryContext context, DataSource dataSource, T @object) where T : class
		{

		}

		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object to delete</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}
		#endregion

		#region Delete (many)
		/// <summary>
		/// Delete the record of the objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{

		}

		/// <summary>
		/// Delete the record of the objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.CompletedTask;
		}
		#endregion

		#region Select
		/// <summary>
		/// Finds all the matched records and return the collection of <see cref="DataRow">DataRow</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<DataRow> Select<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			return new List<DataRow>();
		}

		/// <summary>
		/// Finds all the matched records and return the collection of <see cref="DataRow">DataRow</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<DataRow>> SelectAsync<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<DataRow>>(new List<DataRow>());
		}
		#endregion

		#region Select (identities)
		/// <summary>
		/// Finds all the matched records and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			return SqlHelper.Select(context, dataSource, null, filter, sort, pageSize, pageNumber)
				.Select(data => data["ID"] as string)
				.ToList();
		}

		/// <summary>
		/// Finds all the matched records and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return (await SqlHelper.SelectAsync(context, dataSource, null, filter, sort, pageSize, pageNumber))
				.Select(data => data["ID"] as string)
				.ToList();
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			return new List<T>();
		}

		/// <summary>
		/// Finds the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<T>>(new List<T>());
		}
		#endregion

		#region Find (by identities)
		/// <summary>
		/// Finds the records and construct the collection of objects that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessEntityID = null) where T : class
		{
			if (identities == null || identities.Count < 1)
				return new List<T>();

			var filter = Filters<T>.Or();
			identities.ForEach(id =>
			{
				filter.Add(Filters<T>.Equals("ID", id));
			});

			return SqlHelper.Find(context, dataSource, filter, sort, 0, 1);
		}

		/// <summary>
		/// Finds the records and construct the collection of objects that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (identities == null || identities.Count < 1)
				return Task.FromResult<List<T>>(new List<T>());

			var filter = Filters<T>.Or();
			identities.ForEach(id =>
			{
				filter.Add(Filters<T>.Equals("ID", id));
			});

			return SqlHelper.FindAsync(context, dataSource, filter, sort, 0, 1, businessEntityID, cancellationToken);
		}
		#endregion

		#region Search
		/// <summary>
		/// Searchs the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			return new List<T>();
		}

		/// <summary>
		/// Searchs the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<T>>(new List<T>());
		}

		/// <summary>
		/// Searchs all the matched records and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			return new List<string>();
		}

		/// <summary>
		/// Searchs all the matched records and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<string>> SearchIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<List<string>>(new List<string>());
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			return 0;
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<long>(0);
		}
		#endregion

		#region Count (by query)
		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			return 0;
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return Task.FromResult<long>(0);
		}
		#endregion

	}
}