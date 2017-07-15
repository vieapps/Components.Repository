﻿#region Related components
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
	/// Collection of methods for working with SQL database (support Microsoft SQL Server, MySQL, PostgreSQL, Oracle RDBMS and ODBC)
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

		static bool IsMicrosoftSQL(this DbProviderFactory dbProviderFactory)
		{
			var name = dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "";
			return name.Equals("SqlClientFactory");
		}

		static bool IsOracle(this DbProviderFactory dbProviderFactory)
		{
			var name = dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "";
			return name.Equals("OracleClientFactory");
		}

		static bool IsMySQL(this DbProviderFactory dbProviderFactory)
		{
			var name = dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "";
			return name.Equals("MySqlClientFactory");
		}

		static bool IsPostgreSQL(this DbProviderFactory dbProviderFactory)
		{
			var name = dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "";
			return name.Equals("NpgsqlFactory");
		}

		static bool IsGotRowNumber(this DbProviderFactory dbProviderFactory)
		{
			return dbProviderFactory != null && (dbProviderFactory.IsMicrosoftSQL() || dbProviderFactory.IsOracle());
		}

		static bool IsGotLimitOffset(this DbProviderFactory dbProviderFactory)
		{
			return dbProviderFactory != null && (dbProviderFactory.IsMySQL() || dbProviderFactory.IsPostgreSQL());
		}

		static string GetOffsetStatement(this DbProviderFactory dbProviderFactory, int pageSize, int pageNumber = 1)
		{
			return dbProviderFactory != null && dbProviderFactory.IsGotLimitOffset()
				? "LIMIT " + pageSize.ToString() + " OFFSET " + ((pageNumber - 1) * pageSize).ToString()
				: "";
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
		/// <param name="dbProviderFactory">The object that presents information of a database provider factory</param>
		/// <param name="connectionString">The string that presents information to connect with SQL database</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DbProviderFactory dbProviderFactory, string connectionString)
		{
			if (dbProviderFactory == null)
				return null;
			var connection = dbProviderFactory.CreateConnection();
			if (!string.IsNullOrWhiteSpace(connectionString))
				connection.ConnectionString = connectionString;
			return connection;
		}

		/// <summary>
		/// Gets the connection for working with SQL database
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <param name="dbProviderFactory">The object that presents information of a database provider factory</param>
		/// <returns></returns>
		public static DbConnection GetConnection(DataSource dataSource, DbProviderFactory dbProviderFactory = null)
		{		
			var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? RepositoryMediator.GetConnectionStringSettings(dataSource)
				: null;
			return SqlHelper.GetConnection(dbProviderFactory != null ? dbProviderFactory : SqlHelper.GetProviderFactory(dataSource), connectionStringSettings != null ? connectionStringSettings.ConnectionString : null);
		}
		#endregion

		#region Command
		static DbCommand CreateCommand(this DbConnection connection, Tuple<string, List<DbParameter>> info)
		{
			var command = connection.CreateCommand();
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter =>
			{
				command.Parameters.Add(parameter);
			});
			return command;
		}

		static DbCommand CreateCommand(this DbProviderFactory dbProviderFactory, Tuple<string, List<DbParameter>> info, DbConnection connection = null)
		{
			if (connection != null)
				return connection.CreateCommand(info);
			else
			{
				var command = dbProviderFactory.CreateCommand();
				command.Connection = connection;
				command.CommandText = info.Item1;
				info.Item2.ForEach(parameter =>
				{
					command.Parameters.Add(parameter);
				});
				return command;
			}
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
			return (attribute.Type.IsStringType() && (attribute.Name.EndsWith("ID") || attribute.MaxLength.Equals(32))) || attribute.IsStoredAsString()
				? DbType.AnsiStringFixedLength
				: attribute.IsStoredAsJson()
					? DbType.String
					: SqlHelper.DbTypes[attribute.Type];
		}

		internal static DbType GetDbType(this ExtendedPropertyDefinition attribute)
		{
			return attribute.Type.Equals(typeof(DateTime))
				? DbType.AnsiString
				: SqlHelper.DbTypes[attribute.Type];
		}
		#endregion

		#region Parameter
		static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, KeyValuePair<string, object> info)
		{
			var parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = info.Key;
			parameter.Value = info.Value;
			parameter.DbType = info.Key.EndsWith("ID")
				? DbType.AnsiStringFixedLength
				: SqlHelper.DbTypes[info.Value.GetType()];
			return parameter;
		}

		static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory,ObjectService.AttributeInfo attribute, object value)
		{
			var parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = "@" + attribute.Name;
			parameter.DbType = attribute.GetDbType();
			parameter.Value = attribute.IsStoredAsJson() 
				? parameter.Value = value == null
					? ""
					: value.ToJson().ToString(Newtonsoft.Json.Formatting.None)
				: attribute.IsStoredAsString()
					? value == null
						? ""
						: ((DateTime)value).ToDTString()
					: value;
			return parameter;
		}

		static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, ExtendedPropertyDefinition attribute, object value)
		{
			var parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = "@" + attribute.Name;
			parameter.DbType = attribute.GetDbType();
			parameter.Value = attribute.Type.Equals(typeof(DateTime))
					? value == null
						? ""
						: ((DateTime)value).ToDTString()
					: value;
			return parameter;
		}
		#endregion

		#region Copy (DataReader)
		static T Copy<T>(this T @object, DbDataReader reader, Dictionary<string, ObjectService.AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			// create object
			@object = @object != null
				? @object
				: ObjectService.CreateInstance<T>();

			if (@object is IBusinessEntity && extendedProperties != null &&(@object as IBusinessEntity).ExtendedProperties == null)
				(@object as IBusinessEntity).ExtendedProperties = new Dictionary<string, object>();

			// copy data
			for (var index = 0; index < reader.FieldCount; index++)
			{
				var name = reader.GetName(index);
				if (standardProperties != null && standardProperties.ContainsKey(name))
				{
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
				else if (extendedProperties != null && extendedProperties.ContainsKey(name))
				{
					var attribute = extendedProperties[name];
					var value = reader[index];
					if (value != null && attribute.Type.IsDateTimeType())
						value = DateTime.Parse(value as string);

					if ((@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name))
						(@object as IBusinessEntity).ExtendedProperties[attribute.Name] = value.CastAs(attribute.Type);
					else
						(@object as IBusinessEntity).ExtendedProperties.Add(attribute.Name, value.CastAs(attribute.Type));
				}
			}

			// return object
			return @object;
		}
		#endregion

		#region Copy (DataRow)
		static T Copy<T>(this T @object, DataRow data, Dictionary<string, ObjectService.AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			@object = @object != null
				? @object
				: ObjectService.CreateInstance<T>();

			if (@object is IBusinessEntity && extendedProperties != null && (@object as IBusinessEntity).ExtendedProperties == null)
				(@object as IBusinessEntity).ExtendedProperties = new Dictionary<string, object>();

			for (var index = 0; index < data.Table.Columns.Count; index++)
			{
				var name = data.Table.Columns[index].ColumnName;
				if (standardProperties != null && standardProperties.ContainsKey(name))
				{
					var attribute = standardProperties[name];
					var value = data[name];
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
				else if (extendedProperties != null && extendedProperties.ContainsKey(name))
				{
					var attribute = extendedProperties[name];
					var value = data[name];
					if (value != null && attribute.Type.IsDateTimeType())
						value = DateTime.Parse(value as string);

					if ((@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name))
						(@object as IBusinessEntity).ExtendedProperties[attribute.Name] = value.CastAs(attribute.Type);
					else
						(@object as IBusinessEntity).ExtendedProperties.Add(attribute.Name, value.CastAs(attribute.Type));
				}
			}

			return @object;
		}
		#endregion

		#region Create
		static Tuple<string, List<DbParameter>> PrepareCreateOrigin<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var values = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach(var attribute in definition.Attributes)
			{
				var value = @object.GetAttributeValue(attribute.Name);
				if (value == null && attribute.IsIgnoredIfNull())
					continue;

				columns.Add(string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column);
				values.Add("@" + attribute.Name);
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			var statement = "INSERT INTO " + definition.TableName
				+ " (" + string.Join(", ", columns) + ") VALUES (" + string.Join(", ", values) + ")";

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareCreateExtent<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = "ID,SystemID,RepositoryID,EntityID".ToList();
			var values = "@ID,@SystemID,@RepositoryID,@EntityID".ToList();
			var parameters = new List<DbParameter>()
			{
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID",(@object as IBusinessEntity).ID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@SystemID",(@object as IBusinessEntity).SystemID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@RepositoryID",(@object as IBusinessEntity).RepositoryID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@EntityID",(@object as IBusinessEntity).EntityID))
			};

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var attributes = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
			foreach (var attribute in attributes)
			{
				columns.Add(attribute.Column);
				values.Add("@" + attribute.Name);

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name)
					? (@object as IBusinessEntity).ExtendedProperties[attribute.Name]
					: attribute.GetDefaultValue();
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			var statement = "INSERT INTO " + definition.RepositoryDefinition.ExtendedPropertiesTableName
				+ " (" + string.Join(", ", columns) + ") VALUES (" + string.Join(", ", values) + ")";

			return new Tuple<string, List<DbParameter>>(statement, parameters);
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

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = connection.CreateCommand(@object.PrepareCreateOrigin(dbProviderFactory));
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareCreateExtent(dbProviderFactory));
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

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = connection.CreateCommand(@object.PrepareCreateOrigin(dbProviderFactory));
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareCreateExtent(dbProviderFactory));
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}
		#endregion

		#region Get
		static Tuple<string, List<DbParameter>> PrepareGetOrigin<T>(this T @object, string id, DbProviderFactory dbProviderFactory) where T : class
		{
			var fields = new List<string>();
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach (var attribute in definition.Attributes)
			{
				if (attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) == null)
					continue;

				fields.Add("Origin." + (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column + " AS " + attribute.Name));
			};

			var info = Filters<T>.Equals(definition.PrimaryKey, id).GetSqlStatement();
			var statement = "SELECT " + string.Join(", ", fields)
				+ " FROM " + definition.TableName + " AS Origin WHERE " + info.Item1;
			var parameters = info.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList();

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareGetExtent<T>(this T @object, string id, DbProviderFactory dbProviderFactory, List<ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			var fields = new List<string>() { "Origin.ID" };
			foreach (var attribute in extendedProperties)
				fields.Add("Origin." + attribute.Column + " AS " + attribute.Name);

			var info = Filters<T>.Equals("ID", id).GetSqlStatement();
			var statement = "SELECT " + string.Join(", ", fields)
				+ " FROM " + RepositoryMediator.GetEntityDefinition<T>().RepositoryDefinition.ExtendedPropertiesTableName + " AS Origin WHERE " + info.Item1;
			var parameters = info.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList();

			return new Tuple<string, List<DbParameter>>(statement, parameters);
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
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = connection.CreateCommand(@object.PrepareGetOrigin<T>(id, dbProviderFactory));
				using (var reader = command.ExecuteReader())
				{
					if (reader.Read())
						@object = @object.Copy<T>(reader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = connection.CreateCommand(@object.PrepareGetExtent<T>(id, dbProviderFactory, extendedProperties));
					using (var reader = command.ExecuteReader())
					{
						if (reader.Read())
							@object = @object.Copy<T>(reader, null, extendedProperties.ToDictionary(attribute => attribute.Name));
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
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = connection.CreateCommand(@object.PrepareGetOrigin<T>(id, dbProviderFactory));
				using (var reader = await command.ExecuteReaderAsync(cancellationToken))
				{
					if (await reader.ReadAsync(cancellationToken))
						@object = @object.Copy<T>(reader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = connection.CreateCommand(@object.PrepareGetExtent<T>(id, dbProviderFactory, extendedProperties));
					using (var reader = await command.ExecuteReaderAsync(cancellationToken))
					{
						if (await reader.ReadAsync(cancellationToken))
							@object = @object.Copy<T>(reader, null, extendedProperties.ToDictionary(attribute => attribute.Name));
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
			var objects = context.Find(dataSource, filter, sort, 1, 1, businessEntityID, false);
			return objects != null && objects.Count > 0
				? objects[0]
				: null;
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
		public static async Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var objects = await context.FindAsync(dataSource, filter, sort, 1, 1, businessEntityID, false, cancellationToken);
			return objects != null && objects.Count > 0
				? objects[0]
				: null;
		}
		#endregion

		#region Replace
		static Tuple<string, List<DbParameter>> PrepareReplaceOrigin<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach (var attribute in definition.Attributes)
			{
				var value = @object.GetAttributeValue(attribute.Name);
				if (attribute.Name.Equals(definition.PrimaryKey) || (value == null && attribute.IsIgnoredIfNull()))
					continue;

				columns.Add((string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column) + "=" + "@" + attribute.Name);
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID())));
			var statement = "UPDATE " + definition.TableName
				+ " SET " + string.Join(", ", columns) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareReplaceExtent<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var attributes = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
			foreach (var attribute in attributes)
			{
				columns.Add(attribute.Column + "=@" + attribute.Name);

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name)
					? (@object as IBusinessEntity).ExtendedProperties[attribute.Name]
					: attribute.GetDefaultValue();
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", @object.GetEntityID())));
			var statement = "UPDATE " + definition.RepositoryDefinition.ExtendedPropertiesTableName
				+ " SET " + string.Join(", ", columns) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		/// <summary>
		/// Replaces the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		public static void Replace<T>(this RepositoryContext context, DataSource dataSource, T @object) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot replace because the object is null");

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = connection.CreateCommand(@object.PrepareReplaceOrigin(dbProviderFactory));
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareReplaceExtent(dbProviderFactory));
					command.ExecuteNonQuery();
				}
			}
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
		public static async Task ReplaceAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot replace because the object is null");

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = connection.CreateCommand(@object.PrepareReplaceOrigin(dbProviderFactory));
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareReplaceExtent(dbProviderFactory));
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}
		#endregion

		#region Update
		static Tuple<string, List<DbParameter>> PrepareUpdateOrigin<T>(this T @object, List<string> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var standardProperties = definition.Attributes.ToDictionary(attribute => attribute.Name);
			foreach (var attribute in attributes)
			{
				if (!standardProperties.ContainsKey(attribute))
					continue;

				var value = @object.GetAttributeValue(attribute);
				if (value == null && standardProperties[attribute].IsIgnoredIfNull())
					continue;

				columns.Add((string.IsNullOrEmpty(standardProperties[attribute].Column) ? standardProperties[attribute].Name : standardProperties[attribute].Column) + "=" + "@" + standardProperties[attribute].Name);
				parameters.Add(dbProviderFactory.CreateParameter(standardProperties[attribute], value));
			}

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID())));
			var statement = "UPDATE " + definition.TableName
				+ " SET " + string.Join(", ", columns) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareUpdateExtent<T>(this T @object, List<string> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var colums = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var extendedProperties = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name);
			foreach (var attribute in attributes)
			{
				if (!extendedProperties.ContainsKey(attribute))
					continue;

				colums.Add(extendedProperties[attribute].Column + "=@" + extendedProperties[attribute].Name);

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(extendedProperties[attribute].Name)
					? (@object as IBusinessEntity).ExtendedProperties[extendedProperties[attribute].Name]
					: extendedProperties[attribute].GetDefaultValue();
				parameters.Add(dbProviderFactory.CreateParameter(extendedProperties[attribute], value));
			}

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)));
			var statement = "UPDATE " + definition.RepositoryDefinition.ExtendedPropertiesTableName
				+ " SET " + string.Join(", ", colums) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

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
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = connection.CreateCommand(@object.PrepareUpdateOrigin(attributes, dbProviderFactory));
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareUpdateExtent(attributes, dbProviderFactory));
					command.ExecuteNonQuery();
				}
			}
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
		public static async Task UpdateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = connection.CreateCommand(@object.PrepareUpdateOrigin(attributes, dbProviderFactory));
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareUpdateExtent(attributes, dbProviderFactory));
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}
		#endregion

		#region Delete
		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object to delete</param>
		public static void Delete<T>(this RepositoryContext context, DataSource dataSource, T @object) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var statement = "DELETE FROM " + definition.TableName + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID()))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					statement = "DELETE FROM " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", @object.GetEntityID()))
					};
					command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
					command.ExecuteNonQuery();
				}
			}
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
		public static async Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var statement = "DELETE FROM " + definition.TableName + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID()))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					statement = "DELETE FROM " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", @object.GetEntityID()))
					};
					command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}

		/// <summary>
		/// Delete the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		public static void Delete<T>(this RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			if (string.IsNullOrWhiteSpace(id))
				return;

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var statement = "DELETE FROM " + definition.TableName + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, id))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				command.ExecuteNonQuery();

				if (definition.Extendable && definition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(definition.RepositoryDefinition.ExtendedPropertiesTableName))
				{
					statement = "DELETE FROM " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", id))
					};
					command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
					command.ExecuteNonQuery();
				}
			}
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
		public static async Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (string.IsNullOrWhiteSpace(id))
				return;

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var statement = "DELETE FROM " + definition.TableName + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, id))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (definition.Extendable && definition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(definition.RepositoryDefinition.ExtendedPropertiesTableName))
				{
					statement = "DELETE FROM " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", id))
					};
					command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
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
		static Tuple<string, List<DbParameter>> PrepareSelect<T>(this DbProviderFactory dbProviderFactory, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, definition, true);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var parentIDs = definition != null && autoAssociateWithMultipleParents && filter != null
				? filter.GetAssociatedParentIDs(definition)
				: null;
			var gotAssociateWithMultipleParents = parentIDs != null && parentIDs.Count > 0;

			var statementsInfo = RestrictionsHelper.PrepareSqlStatements<T>(filter, sort, businessEntityID, autoAssociateWithMultipleParents, definition, parentIDs, propertiesInfo);

			// fields/columns (SELECT)
			var fields = new List<string>();
			(attributes != null && attributes.Count() > 0
				? attributes
				: standardProperties
					.Select(item => item.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(item => item.Value.Name) : new List<string>())
			).ForEach(attribute =>
			{
				if (standardProperties.ContainsKey(attribute.ToLower()) || (extendedProperties != null && extendedProperties.ContainsKey(attribute.ToLower())))
					fields.Add(attribute);
			});

			var columns = fields.Select(field =>
				extendedProperties != null && extendedProperties.ContainsKey(field.ToLower())
				? "Extent." + extendedProperties[field.ToLower()].Column + " AS " + extendedProperties[field.ToLower()].Name
				: "Origin." + (string.IsNullOrWhiteSpace(standardProperties[field.ToLower()].Column)
					? standardProperties[field.ToLower()].Name
					: standardProperties[field.ToLower()].Column + " AS " + standardProperties[field.ToLower()].Name)
				)
				.ToList();

			// tables (FROM)
			var tables = " FROM " + definition.TableName + " AS Origin"
				+ (extendedProperties != null ? " LEFT JOIN " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " AS Extent ON Origin." + definition.PrimaryKey + "=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? " LEFT JOIN " + definition.MultipleParentAssociatesTable + " AS Link ON Origin." + definition.PrimaryKey + "=Link." + definition.MultipleParentAssociatesLinkColumn : "");

			// filtering expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// ordering expressions (ORDER BY)
			string orderby = statementsInfo.Item2;

			// statements
			var select = "SELECT " + (gotAssociateWithMultipleParents ? "DISTINCT " : "") + string.Join(", ", columns) + tables + where;
			var statement = "";

			// pagination with ROW_NUMBER
			if (pageSize > 0 && dbProviderFactory.IsGotRowNumber())
			{
				// normalize: add name of extended column into ORDER BY clause
				if (!string.IsNullOrWhiteSpace(orderby))
				{
					var orders = orderby.ToArray(',');
					orderby = "";
					orders.ForEach(order =>
					{
						var info = order.ToArray(' ');
						if (extendedProperties != null && extendedProperties.ContainsKey(info[0].ToLower()))
							orderby += (orderby.Equals("") ? "" : ", ")
								+ extendedProperties[info[0]].Column
								+ (info.Length > 1 ? " " + info[1] : "");
						else
							orderby += (orderby.Equals("") ? "" : ", ") + order;
					});
				}

				// set pagination statement
				statement = "SELECT " + string.Join(", ", fields) + ","
					+ " ROW_NUMBER() OVER(ORDER BY " + (!string.IsNullOrWhiteSpace(orderby) ? orderby : definition.PrimaryKey + " ASC") + ") AS __RowNumber"
					+ " FROM (" + select + ") AS __DistinctResults";

				statement = "SELECT " + string.Join(", ", fields)
					+ " FROM (" + statement + ") AS __Results"
					+ " WHERE __Results.__RowNumber > " + ((pageNumber - 1) * pageSize).ToString() + " AND __Results.__RowNumber <= " + (pageNumber * pageSize).ToString()
					+ " ORDER BY __Results.__RowNumber";
			}

			// no pagination or pagination with generic SQL/got LIMIT ... OFFSET ...
			else
				statement = select
					+ (!string.IsNullOrWhiteSpace(orderby) ? " ORDER BY " + orderby : "")
					+ (pageSize > 0 ? dbProviderFactory.GetOffsetStatement(pageSize, pageNumber) : "");

			// parameters
			var parameters = statementsInfo.Item1 != null && statementsInfo.Item1.Item2 != null
				? statementsInfo.Item1.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList()
				: new List<DbParameter>();

			// return information
			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static List<DataRow> ToList(this DataRowCollection dataRows)
		{
			var rows = new List<DataRow>();
			foreach (DataRow row in dataRows)
				rows.Add(row);
			return rows;
		}

		static void Copy(this DataTable dataTable, DbDataReader reader)
		{
			while (reader.Read())
			{
				object[] data = new object[reader.FieldCount];
				for (int index = 0; index < reader.FieldCount; index++)
					data[index] = reader[index];
				dataTable.LoadDataRow(data, true);
			}
		}

		static async Task CopyAsync(this DataTable dataTable, DbDataReader reader, CancellationToken cancellationToken = default(CancellationToken))
		{
			while (await reader.ReadAsync(cancellationToken))
			{
				object[] data = new object[reader.FieldCount];
				for (int index = 0; index < reader.FieldCount; index++)
					data[index] = reader[index];
				dataTable.LoadDataRow(data, true);
			}
		}

		static DataTable CreateDataTable(this DbDataReader reader, string name = "Table", bool doLoad = false)
		{
			var dataTable = new DataTable(name);

			if (doLoad)
				dataTable.Load(reader);

			else
				foreach (DataRow info in reader.GetSchemaTable().Rows)
				{
					var column = new DataColumn();
					column.ColumnName = info["ColumnName"].ToString();
					column.Unique = Convert.ToBoolean(info["IsUnique"]);
					column.AllowDBNull = Convert.ToBoolean(info["AllowDBNull"]);
					column.ReadOnly = Convert.ToBoolean(info["IsReadOnly"]);
					column.DataType = (Type)info["DataType"];
					dataTable.Columns.Add(column);
				}

			return dataTable;
		}

		static DataTable GetDataTable(this DbDataReader reader, string name = "Table", bool doLoad = false)
		{
			var dataTable = reader.CreateDataTable(name, doLoad);
			if (!doLoad)
				dataTable.Copy(reader);
			return dataTable;
		}

		static async Task<DataTable> GetDataTableAsync(this DbDataReader reader, string name = "Table", bool doLoad = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			var dataTable = reader.CreateDataTable(name, doLoad);
			if (!doLoad)
				await dataTable.CopyAsync(reader, cancellationToken);
			return dataTable;
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
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static List<DataRow> Select<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var info = dbProviderFactory.PrepareSelect<T>(attributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents);
				DataTable dataTable = null;

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var reader = command.ExecuteReader())
					{
						dataTable = reader.GetDataTable(typeof(T).GetTypeName(true));
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
					{
						var startRecord = pageNumber > 0 ? (pageNumber - 1) * pageSize : 0;
						dataAdapter.Fill(dataSet, startRecord, pageSize, typeof(T).GetTypeName(true));
					}
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));
					dataTable = dataSet.Tables[0];
				}

				return dataTable.Rows.ToList();
			}
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
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<DataRow>> SelectAsync<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var info = dbProviderFactory.PrepareSelect<T>(attributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents);
				DataTable dataTable = null;

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var reader = await command.ExecuteReaderAsync())
					{
						dataTable = await reader.GetDataTableAsync(typeof(T).GetTypeName(true), false, cancellationToken);
					}
				}

				// generic SQL
				else
				{
					var startRecord = (pageNumber - 1) * pageSize;
					if (startRecord < 0)
						startRecord = 0;

					var dataSet = new DataSet();
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);
					dataAdapter.Fill(dataSet, startRecord, pageSize, typeof(T).GetTypeName(true));
					dataTable = dataSet.Tables[0];
				}

				return dataTable.Rows.ToList();
			}
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
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			return SqlHelper.Select(context, dataSource, new List<string>() { context.EntityDefinition.PrimaryKey }, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
				.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
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
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return (await SqlHelper.SelectAsync(context, dataSource, new List<string>() { context.EntityDefinition.PrimaryKey }, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken))
				.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
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
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			var standardProperties = context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name);
			var extendedProperties = !string.IsNullOrWhiteSpace(businessEntityID) && context.EntityDefinition.RuntimeEntities.ContainsKey(businessEntityID)
				? context.EntityDefinition.RuntimeEntities[businessEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name)
				: null;

			if (autoAssociateWithMultipleParents && context.EntityDefinition.ParentType != null && !string.IsNullOrWhiteSpace(context.EntityDefinition.ParentAssociatedProperty))
			{
				var allAttributes = standardProperties
					.Select(info => info.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>())
					.ToList();

				var distinctAttributes = (new List<string>(sort != null ? sort.GetAttributes() : new List<string>()) { context.EntityDefinition.PrimaryKey })
					.Distinct()
					.ToList();

				var objects = context.Select<T>(dataSource, distinctAttributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToDictionary(@object => @object.GetEntityID(context.EntityDefinition.PrimaryKey));

				var otherAttributes = allAttributes.Except(distinctAttributes).ToList();
				if (otherAttributes.Count > 0)
				{
					otherAttributes.Add(context.EntityDefinition.PrimaryKey);
					context.Select<T>(dataSource, otherAttributes, Filters<T>.Or(objects.Select(item => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, item.Key))), null, 0, 1, businessEntityID, false)
						.ForEach(data =>
						{
							var id = data[context.EntityDefinition.PrimaryKey].CastAs<string>();
							objects[id] = objects[id].Copy(data, standardProperties, extendedProperties);
						});
				}

				return objects.Select(item => item.Value).ToList();
			}
			else
				return context.Select<T>(dataSource, null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToList();
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
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var standardProperties = context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name);
			var extendedProperties = !string.IsNullOrWhiteSpace(businessEntityID) && context.EntityDefinition.RuntimeEntities.ContainsKey(businessEntityID)
				? context.EntityDefinition.RuntimeEntities[businessEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name)
				: null;

			if (autoAssociateWithMultipleParents && context.EntityDefinition.ParentType != null && !string.IsNullOrWhiteSpace(context.EntityDefinition.ParentAssociatedProperty))
			{
				var allAttributes = standardProperties
					.Select(info => info.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>())
					.ToList();

				var distinctAttributes = (new List<string>(sort != null ? sort.GetAttributes() : new List<string>()) { context.EntityDefinition.PrimaryKey })
					.Distinct()
					.ToList();

				var objects = (await context.SelectAsync<T>(dataSource, distinctAttributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken))
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToDictionary(@object => @object.GetEntityID(context.EntityDefinition.PrimaryKey));

				var otherAttributes = allAttributes.Except(distinctAttributes).ToList();
				if (otherAttributes.Count > 0)
				{
					otherAttributes.Add(context.EntityDefinition.PrimaryKey);
					(await context.SelectAsync<T>(dataSource, otherAttributes, Filters<T>.Or(objects.Select(item => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, item.Key))), null, 0, 1, businessEntityID, false, cancellationToken))
						.ForEach(data =>
						{
							var id = data[context.EntityDefinition.PrimaryKey].CastAs<string>();
							objects[id] = objects[id].Copy(data, standardProperties, extendedProperties);
						});
				}

				return objects.Select(item => item.Value).ToList();
			}
			else
				return (await context.SelectAsync<T>(dataSource, null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken))
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToList();
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
			return identities == null || identities.Count < 1
				? new List<T>()
				: context.Find(dataSource, Filters<T>.Or(identities.Select(id => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, id))), sort, 0, 1, businessEntityID, false);
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
			return identities == null || identities.Count < 1
				? Task.FromResult<List<T>>(new List<T>())
				: context.FindAsync(dataSource, Filters<T>.Or(identities.Select(id => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, id))), sort, 0, 1, businessEntityID, false, cancellationToken);
		}
		#endregion

		#region Count
		static Tuple<string, List<DbParameter>> PrepareCount<T>(this DbProviderFactory dbProviderFactory, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, definition);

			var parentIDs = definition != null && autoAssociateWithMultipleParents && filter != null
				? filter.GetAssociatedParentIDs(definition)
				: null;
			var gotAssociateWithMultipleParents = parentIDs != null && parentIDs.Count > 0;

			var statementsInfo = RestrictionsHelper.PrepareSqlStatements<T>(filter, null, businessEntityID, autoAssociateWithMultipleParents, definition, parentIDs, propertiesInfo);

			// tables (FROM)
			var tables = " FROM " + definition.TableName + " AS Origin"
				+ (propertiesInfo.Item2 != null ? " LEFT JOIN " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " AS Extent ON Origin." + definition.PrimaryKey + "=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? " LEFT JOIN " + definition.MultipleParentAssociatesTable + " AS Link ON Origin." + definition.PrimaryKey + "=Link." + definition.MultipleParentAssociatesLinkColumn : "");

			// couting expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// statement
			var statement = "SELECT COUNT(" + (gotAssociateWithMultipleParents ? "DISTINCT " : "") + definition.PrimaryKey + ") AS TotalRecords" + tables + where;

			// parameters
			var parameters = statementsInfo.Item1 != null && statementsInfo.Item1.Item2 != null
				? statementsInfo.Item1.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList()
				: new List<DbParameter>();

			// return info
			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = connection.CreateCommand(dbProviderFactory.PrepareCount<T>(filter, businessEntityID, autoAssociateWithMultipleParents));
				return command.ExecuteScalar().CastAs<long>();
			}
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = connection.CreateCommand(dbProviderFactory.PrepareCount<T>(filter, businessEntityID, autoAssociateWithMultipleParents));
				return (await command.ExecuteScalarAsync(cancellationToken)).CastAs<long>();
			}
		}
		#endregion

		#region Search
		/// <summary>
		/// Gets the terms for searching in SQL database using full-text search
		/// </summary>
		/// <param name="dbProviderFactory"></param>
		/// <param name="queryInfo"></param>
		/// <returns></returns>
		public static string GetSearchTerms(this DbProviderFactory dbProviderFactory, SearchQuery queryInfo)
		{
			var searchTerms = "";

			// Microsoft SQL Server
			if (dbProviderFactory.IsMicrosoftSQL())
			{
				// prepare AND/OR/AND NOT terms
				var andSearchTerms = "";
				queryInfo.AndWords.ForEach(word =>
				{
					andSearchTerms += "\"*" + word + "*\"" + " AND ";
				});
				queryInfo.AndPhrases.ForEach(phrase =>
				{
					andSearchTerms += "\"" + phrase + "\" AND ";
				});
				if (!andSearchTerms.Equals(""))
					andSearchTerms = andSearchTerms.Left(andSearchTerms.Length - 5);

				var notSearchTerms = "";
				queryInfo.NotWords.ForEach(word =>
				{
					notSearchTerms += " AND NOT " + word;
				});
				queryInfo.NotPhrases.ForEach(phrase =>
				{
					notSearchTerms += " AND NOT \"" + phrase + "\"";
				});
				if (!notSearchTerms.Equals(""))
					notSearchTerms = notSearchTerms.Trim();

				var orSearchTerms = "";
				queryInfo.OrWords.ForEach(word =>
				{
					orSearchTerms += "\"*" + word + "*\"" + " OR ";
				});
				queryInfo.OrPhrases.ForEach(phrase =>
				{
					orSearchTerms += "\"" + phrase + "\" OR ";
				});
				if (!orSearchTerms.Equals(""))
					orSearchTerms = orSearchTerms.Left(orSearchTerms.Length - 4);

				// build search terms
				if (!andSearchTerms.Equals("") && !notSearchTerms.Equals("") && !orSearchTerms.Equals(""))
					searchTerms = andSearchTerms + " " + notSearchTerms + " AND (" + orSearchTerms + ")";

				else if (andSearchTerms.Equals("") && orSearchTerms.Equals("") && !notSearchTerms.Equals(""))
					searchTerms = "";

				else if (andSearchTerms.Equals(""))
				{
					searchTerms = orSearchTerms;
					if (!notSearchTerms.Equals(""))
					{
						if (!searchTerms.Equals(""))
							searchTerms = "(" + searchTerms + ") ";
						searchTerms += notSearchTerms;
					}
				}

				else
				{
					searchTerms = andSearchTerms;
					if (!notSearchTerms.Equals(""))
					{
						if (!searchTerms.Equals(""))
							searchTerms += " ";
						searchTerms += notSearchTerms;
					}
					if (!orSearchTerms.Equals(""))
					{
						if (!searchTerms.Equals(""))
							searchTerms += " AND (" + orSearchTerms + ")";
						else
							searchTerms += orSearchTerms;
					}
				}

				// return search terms with Unicode mark (N')
				searchTerms = "N'" + searchTerms + "'";
			}

			else if (dbProviderFactory.IsMySQL())
			{
				queryInfo.AndWords.ForEach(word =>
				{
					searchTerms += (searchTerms.Equals("") ? "" : " ") + "+" + word;
				});
				queryInfo.AndPhrases.ForEach(phrase =>
				{
					searchTerms += (searchTerms.Equals("") ? "" : " ") + "+\"" + phrase + "\"";
				});

				queryInfo.NotWords.ForEach(word =>
				{
					searchTerms += (searchTerms.Equals("") ? "" : " ") + "-" + word;
				});
				queryInfo.NotPhrases.ForEach(phrase =>
				{
					searchTerms += (searchTerms.Equals("") ? "" : " ") + "-\"" + phrase + "\"";
				});

				queryInfo.OrWords.ForEach(word =>
				{
					searchTerms += (searchTerms.Equals("") ? "" : " ") + word;
				});
				queryInfo.OrPhrases.ForEach(phrase =>
				{
					searchTerms += (searchTerms.Equals("") ? "" : " ") + "\"" + phrase + "\"";
				});
			}

			return searchTerms;
		}

		static Tuple<string, List<DbParameter>> PrepareSearch<T>(this DbProviderFactory dbProviderFactory, IEnumerable<string> attributes, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, string searchInColumns = "*") where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, definition, true);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var statementsInfo = RestrictionsHelper.PrepareSqlStatements<T>(filter, null, businessEntityID, false, definition, null, propertiesInfo);

			// fields/columns (SELECT)
			var fields = new List<string>();
			(attributes != null && attributes.Count() > 0
				? attributes
				: standardProperties
					.Select(item => item.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(item => item.Value.Name) : new List<string>())
			).ForEach(attribute =>
			{
				if (standardProperties.ContainsKey(attribute.ToLower()) || (extendedProperties != null && extendedProperties.ContainsKey(attribute.ToLower())))
					fields.Add(attribute);
			});

			var columns = fields.Select(field =>
				extendedProperties != null && extendedProperties.ContainsKey(field.ToLower())
				? "Extent." + extendedProperties[field.ToLower()].Column + " AS " + extendedProperties[field.ToLower()].Name
				: "Origin." + (string.IsNullOrWhiteSpace(standardProperties[field.ToLower()].Column)
					? standardProperties[field.ToLower()].Name
					: standardProperties[field.ToLower()].Column + " AS " + standardProperties[field.ToLower()].Name)
				)
				.ToList();

			// tables (FROM)
			var tables = " FROM " + definition.TableName + " AS Origin"
				+ (extendedProperties != null ? " LEFT JOIN " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " AS Extent ON Origin." + definition.PrimaryKey + "=Extent.ID" : "");

			// filtering expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// ordering expressions (ORDER BY)
			var orderby = "";

			// searching terms
			var searchTerms = dbProviderFactory.GetSearchTerms(Utility.SearchQuery.Parse(query));

			// Microsoft SQL Server
			if (dbProviderFactory.IsMicrosoftSQL())
			{
				fields.Add("SearchScore");
				columns.Add("Search.[RANK] AS SearchScore");
				tables += " INNER JOIN CONTAINSTABLE (" + definition.TableName + ", " + searchInColumns + ", " + searchTerms + ") AS Search ON Origin." + definition.PrimaryKey + "=Search.[KEY]";
				orderby = "SearchScore DESC";
			}

			// MySQL
			else if (dbProviderFactory.IsMySQL())
			{
				searchInColumns = !searchInColumns.Equals("*")
					? searchInColumns
					: standardProperties
						.Where(attribute => attribute.Value.IsSearchable())
						.Select(attribute => "Origin." + attribute.Value.Name)
						.ToList()
						.ToString(",");

				fields.Add("SearchScore");
				columns.Add("(MATCH(" + searchInColumns + ") AGAINST (" + searchTerms + " IN BOOLEAN MODE) AS SearchScore");
				where += !where.Equals("") ? " AND SearchScore > 0" : " WHERE SearchScore > 0";
				orderby = "SearchScore DESC";
			}

			// statement
			var select = "SELECT " + string.Join(", ", columns) + tables + where;
			var statement = "";

			// pagination with ROW_NUMBER
			if (pageSize > 0 && dbProviderFactory.IsGotRowNumber())
			{
				// normalize: add name of extended column into ORDER BY clause
				if (!string.IsNullOrWhiteSpace(orderby))
				{
					var orders = orderby.ToArray(',');
					orderby = "";
					orders.ForEach(order =>
					{
						var info = order.ToArray(' ');
						if (extendedProperties != null && extendedProperties.ContainsKey(info[0].ToLower()))
							orderby += (orderby.Equals("") ? "" : ", ")
								+ extendedProperties[info[0]].Column
								+ (info.Length > 1 ? " " + info[1] : "");
						else
							orderby += (orderby.Equals("") ? "" : ", ") + order;
					});
				}

				// set pagination statement
				statement = "SELECT " + string.Join(", ", fields) + ","
					+ " ROW_NUMBER() OVER(ORDER BY " + (!string.IsNullOrWhiteSpace(orderby) ? orderby : definition.PrimaryKey + " ASC") + ") AS __RowNumber"
					+ " FROM (" + select + ") AS __DistinctResults";

				statement = "SELECT " + string.Join(", ", fields)
					+ " FROM (" + statement + ") AS __Results"
					+ " WHERE __Results.__RowNumber > " + ((pageNumber - 1) * pageSize).ToString() + " AND __Results.__RowNumber <= " + (pageNumber * pageSize).ToString()
					+ " ORDER BY __Results.__RowNumber";
			}

			// no pagination or pagination with generic SQL/got LIMIT ... OFFSET ...
			else
				statement = select
					+ (!string.IsNullOrWhiteSpace(orderby) ? " ORDER BY " + orderby : "")
					+ (pageSize > 0 ? dbProviderFactory.GetOffsetStatement(pageSize, pageNumber) : "");

			// parameters
			var parameters = statementsInfo.Item1 != null && statementsInfo.Item1.Item2 != null
				? statementsInfo.Item1.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList()
				: new List<DbParameter>();

			// return info
			return new Tuple<string, List<DbParameter>>(statement, parameters);
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
		/// <returns></returns>
		public static List<T> Search<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var objects = new List<T>();
				var info = dbProviderFactory.PrepareSearch<T>(null, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
							objects.Add(ObjectService.CreateInstance<T>().Copy(reader, standardProperties, extendedProperties));
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
					{
						var startRecord = pageNumber > 0 ? (pageNumber - 1) * pageSize : 0;
						dataAdapter.Fill(dataSet, startRecord, pageSize, typeof(T).GetTypeName(true));
					}
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					dataSet.Tables[0].Rows
						.ToList()
						.ForEach(data =>
						{
							objects.Add(ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties));
						});
				}

				return objects;
			}
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
		public static async Task<List<T>> SearchAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var objects = new List<T>();
				var info = dbProviderFactory.PrepareSearch<T>(null, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var reader = await command.ExecuteReaderAsync(cancellationToken))
					{
						while (await reader.ReadAsync(cancellationToken))
							objects.Add(ObjectService.CreateInstance<T>().Copy(reader, standardProperties, extendedProperties));
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
					{
						var startRecord = pageNumber > 0 ? (pageNumber - 1) * pageSize : 0;
						dataAdapter.Fill(dataSet, startRecord, pageSize, typeof(T).GetTypeName(true));
					}
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					dataSet.Tables[0].Rows
						.ToList()
						.ForEach(data =>
						{
							objects.Add(ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties));
						});
				}

				return objects;
			}
		}
		#endregion

		#region Search (identities)
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
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var identities = new List<string>();
				var info = dbProviderFactory.PrepareSearch<T>(new List<string>() { context.EntityDefinition.PrimaryKey }, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
							identities.Add(reader[context.EntityDefinition.PrimaryKey].CastAs<string>());
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
					{
						var startRecord = pageNumber > 0 ? (pageNumber - 1) * pageSize : 0;
						dataAdapter.Fill(dataSet, startRecord, pageSize, typeof(T).GetTypeName(true));
					}
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					dataSet.Tables[0].Rows
						.ToList()
						.ForEach(data =>
						{
							identities.Add(data[context.EntityDefinition.PrimaryKey].CastAs<string>());
						});
				}

				return identities;
			}
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
		public static async Task<List<string>> SearchIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var identities = new List<string>();
				var info = dbProviderFactory.PrepareSearch<T>(new List<string>() { context.EntityDefinition.PrimaryKey }, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var reader = await command.ExecuteReaderAsync(cancellationToken))
					{
						while (await reader.ReadAsync(cancellationToken))
							identities.Add(reader[context.EntityDefinition.PrimaryKey].CastAs<string>());
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
					{
						var startRecord = pageNumber > 0 ? (pageNumber - 1) * pageSize : 0;
						dataAdapter.Fill(dataSet, startRecord, pageSize, typeof(T).GetTypeName(true));
					}
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					dataSet.Tables[0].Rows
						.ToList()
						.ForEach(data =>
						{
							identities.Add(data[context.EntityDefinition.PrimaryKey].CastAs<string>());
						});
				}

				return identities;
			}
		}
		#endregion

		#region Count (searching)
		static Tuple<string, List<DbParameter>> PrepareCount<T>(this DbProviderFactory dbProviderFactory, string query, IFilterBy<T> filter, string businessEntityID = null, string searchInColumns = "*") where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, definition, true);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var statementsInfo = RestrictionsHelper.PrepareSqlStatements<T>(filter, null, businessEntityID, false, definition, null, propertiesInfo);

			// tables (FROM)
			var tables = " FROM " + definition.TableName + " AS Origin"
				+ (extendedProperties != null ? " LEFT JOIN " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " AS Extent ON Origin." + definition.PrimaryKey + "=Extent.ID" : "");

			// filtering expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// searching terms
			var searchTerms = dbProviderFactory.GetSearchTerms(Utility.SearchQuery.Parse(query));

			// Microsoft SQL Server
			if (dbProviderFactory.IsMicrosoftSQL())
				tables += " INNER JOIN CONTAINSTABLE (" + definition.TableName + ", " + searchInColumns + ", " + searchTerms + ") AS Search ON Origin." + definition.PrimaryKey + "=Search.[KEY]";

			// MySQL
			else if (dbProviderFactory.IsMySQL())
			{
				searchInColumns = !searchInColumns.Equals("*")
					? searchInColumns
					: standardProperties
						.Where(attribute => attribute.Value.IsSearchable())
						.Select(attribute => "Origin." + attribute.Value.Name)
						.ToList()
						.ToString(",");
				where += (!where.Equals("") ? " AND " : " WHERE ")
					+ "(MATCH(" + searchInColumns + ") AGAINST (" + searchTerms + " IN BOOLEAN MODE) > 0";
			}

			// statement
			var statement = "SELECT COUNT(Origin." + definition.PrimaryKey + ") AS TotalRecords" + tables + where;

			// parameters
			var parameters = statementsInfo.Item1 != null && statementsInfo.Item1.Item2 != null
				? statementsInfo.Item1.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList()
				: new List<DbParameter>();

			// return info
			return new Tuple<string, List<DbParameter>>(statement, parameters);
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
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				connection.Open();
				var command = connection.CreateCommand(dbProviderFactory.PrepareCount<T>(query, filter, businessEntityID));
				return command.ExecuteScalar().CastAs<long>();
			}
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
		public static async Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = SqlHelper.GetConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);
				var command = connection.CreateCommand(dbProviderFactory.PrepareCount<T>(query, filter, businessEntityID));
				return (await command.ExecuteScalarAsync(cancellationToken)).CastAs<long>();
			}
		}
		#endregion

	}
}