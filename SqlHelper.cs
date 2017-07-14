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

		static bool IsGotRowNumber(this DbProviderFactory dbProviderFactory)
		{
			return !dbProviderFactory.GetType().GetTypeName(true).Equals("MySqlClientFactory");
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

		static DbCommand CreateCommand(this Tuple<string, List<DbParameter>> info, DbProviderFactory dbProviderFactory)
		{
			var command = dbProviderFactory.CreateCommand();
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter =>
			{
				command.Parameters.Add(parameter);
			});
			return command;
		}

		static DbParameter CreateParameter(this KeyValuePair<string, object> info, DbProviderFactory dbProviderFactory)
		{
			var parameter = dbProviderFactory.CreateParameter();
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

		#region Copy data from DataReader/DataRow into object
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

		static T Copy<T>(this T @object, DataRow data, Dictionary<string, ObjectService.AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			@object = @object != null
				? @object
				: ObjectService.CreateInstance<T>();

			if (@object is IBusinessEntity && extendedProperties != null && (@object as IBusinessEntity).ExtendedProperties == null)
				(@object as IBusinessEntity).ExtendedProperties = new Dictionary<string, object>();

			if (standardProperties != null)
				standardProperties.ForEach(info =>
				{
					var attribute = info.Value;					
					object value = null;
					if (data.Table.Columns.Contains(attribute.Name))
						try
						{
							value = data[attribute.Name];
						}
						catch { }

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
						@object.SetAttributeValue(attribute, value, true);
					}
				});

			if (extendedProperties != null)
				extendedProperties.ForEach(info =>
				{
					var attribute = info.Value;
					if (data.Table.Columns.Contains(attribute.Name))
					{
						object value = null;
						try
						{
							value = data[attribute.Name];
						}
						catch { }

						if (value != null && attribute.Type.IsDateTimeType())
							value = DateTime.Parse(value as string);

						if ((@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name))
							(@object as IBusinessEntity).ExtendedProperties[attribute.Name] = value.CastAs(attribute.Type);
						else
							(@object as IBusinessEntity).ExtendedProperties.Add(attribute.Name, value.CastAs(attribute.Type));
					}
				});

			return @object;
		}
		#endregion

		#region Create
		static Tuple<string, List<DbParameter>> PrepareCreateOrigin<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
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

				var parameter = dbProviderFactory.CreateParameter();
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

		static Tuple<string, List<DbParameter>> PrepareCreateExtent<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = "ID,SystemID,RepositoryID,EntityID";
			var values = "@ID,@SystemID,@RepositoryID,@EntityID";
			var parameters = new List<DbParameter>();

			var parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = "@ID";
			parameter.Value = (@object as IBusinessEntity).ID;
			parameter.DbType = DbType.StringFixedLength;
			parameters.Add(parameter);

			parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = "@SystemID";
			parameter.Value = (@object as IBusinessEntity).SystemID;
			parameter.DbType = DbType.StringFixedLength;
			parameters.Add(parameter);

			parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = "@RepositoryID";
			parameter.Value = (@object as IBusinessEntity).RepositoryID;
			parameter.DbType = DbType.StringFixedLength;
			parameters.Add(parameter);

			parameter = dbProviderFactory.CreateParameter();
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

				parameter = dbProviderFactory.CreateParameter();
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

			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = @object.PrepareCreateOrigin(dbProviderFactory).CreateCommand(connection);
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareCreateExtent(dbProviderFactory).CreateCommand(connection);
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = @object.PrepareCreateOrigin(dbProviderFactory).CreateCommand(connection);
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareCreateExtent(dbProviderFactory).CreateCommand(connection);
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}
		#endregion

		#region Get
		static Tuple<string, List<DbParameter>> PrepareGetOrigin<T>(this T @object, string id, DbProviderFactory dbProviderFactory) where T : class
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
				new List<DbParameter>(info.Item2.Select(param => param.CreateParameter(dbProviderFactory)))
			);
		}

		static Tuple<string, List<DbParameter>> PrepareGetExtent<T>(this T @object, string id, DbProviderFactory dbProviderFactory, List<ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			var statement = "Origin.ID";
			foreach (var attribute in extendedProperties)
				statement += "Origin." + attribute.Column + " AS " + attribute.Name + ",";

			var info = Filters<T>.Equals("ID", id).GetSqlStatement();
			return new Tuple<string, List<DbParameter>>
			(
				"SELECT TOP 1 " + statement.Left(statement.Length - 1) + " FROM " + RepositoryMediator.GetEntityDefinition<T>().RepositoryDefinition.ExtendedPropertiesTableName + " AS Origin WHERE " + info.Item1,
				new List<DbParameter>(info.Item2.Select(param => param.CreateParameter(dbProviderFactory)))
			);
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = @object.PrepareGetOrigin<T>(id, dbProviderFactory).CreateCommand(connection);
				using (var reader = command.ExecuteReader())
				{
					if (reader.Read())
						@object = @object.Copy<T>(reader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = @object.PrepareGetExtent<T>(id, dbProviderFactory, extendedProperties).CreateCommand(connection);
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = @object.PrepareGetOrigin<T>(id, dbProviderFactory).CreateCommand(connection);
				using (var reader = await command.ExecuteReaderAsync(cancellationToken))
				{
					if (await reader.ReadAsync(cancellationToken))
						@object = @object.Copy<T>(reader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = @object.PrepareGetExtent<T>(id, dbProviderFactory, extendedProperties).CreateCommand(connection);
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
			var statement = "";
			var parameters = new List<DbParameter>();
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach (var attribute in definition.Attributes)
			{
				var value = @object.GetAttributeValue(attribute.Name);
				if (attribute.Name.Equals(definition.PrimaryKey) || (value == null && attribute.IsIgnoredIfNull()))
					continue;

				statement += (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column) + "=" + "@" + attribute.Name + ",";

				var parameter = dbProviderFactory.CreateParameter();
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

			statement = "UPDATE " + definition.TableName + " SET " + statement.Left(statement.Length - 1) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
			parameters.Add((new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID())).CreateParameter(dbProviderFactory));

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareReplaceExtent<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var statement = "";
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var attributes = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
			foreach (var attribute in attributes)
			{
				statement += attribute.Column + "=@" + attribute.Name + ",";

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name)
					? (@object as IBusinessEntity).ExtendedProperties[attribute.Name]
					: attribute.GetDefaultValue();

				var parameter = dbProviderFactory.CreateParameter();
				parameter.ParameterName = "@" + attribute.Name;
				parameter.DbType = attribute.DbType;
				parameter.Value = attribute.Type.Equals(typeof(DateTime))
					? ((DateTime)value).ToDTString()
					: value;
				parameters.Add(parameter);
			}

			statement = "UPDATE " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " SET " + statement.Left(statement.Length - 1) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
			parameters.Add((new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)).CreateParameter(dbProviderFactory));

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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = @object.PrepareReplaceOrigin(dbProviderFactory).CreateCommand(connection);
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareReplaceExtent(dbProviderFactory).CreateCommand(connection);
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = @object.PrepareReplaceOrigin(dbProviderFactory).CreateCommand(connection);
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareReplaceExtent(dbProviderFactory).CreateCommand(connection);
					await command.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}
		#endregion

		#region Update
		static Tuple<string, List<DbParameter>> PrepareUpdateOrigin<T>(this T @object, List<string> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var statement = "";
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

				statement += (string.IsNullOrEmpty(standardProperties[attribute].Column) ? standardProperties[attribute].Name : standardProperties[attribute].Column) + "=" + "@" + standardProperties[attribute].Name + ",";

				var parameter = dbProviderFactory.CreateParameter();
				parameter.ParameterName = "@" + standardProperties[attribute].Name;
				if (standardProperties[attribute].IsStoredAsJson())
				{
					parameter.DbType = DbType.String;
					parameter.Value = value.ToJson().ToString(Newtonsoft.Json.Formatting.None);
				}
				else
				{
					parameter.DbType = standardProperties[attribute].GetDbType();
					parameter.Value = standardProperties[attribute].IsStoredAsString()
						? ((DateTime)value).ToDTString()
						: value;
				}
				parameters.Add(parameter);
			}

			statement = "UPDATE " + definition.TableName + " SET " + statement.Left(statement.Length - 1) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
			parameters.Add((new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID())).CreateParameter(dbProviderFactory));

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareUpdateExtent<T>(this T @object, List<string> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var statement = "";
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var extendedProperties = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name);
			foreach (var attribute in attributes)
			{
				if (!extendedProperties.ContainsKey(attribute))
					continue;

				statement += extendedProperties[attribute].Column + "=@" + extendedProperties[attribute].Name + ",";

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(extendedProperties[attribute].Name)
					? (@object as IBusinessEntity).ExtendedProperties[extendedProperties[attribute].Name]
					: extendedProperties[attribute].GetDefaultValue();

				var parameter = dbProviderFactory.CreateParameter();
				parameter.ParameterName = "@" + extendedProperties[attribute].Name;
				parameter.DbType = extendedProperties[attribute].DbType;
				parameter.Value = extendedProperties[attribute].Type.Equals(typeof(DateTime))
					? ((DateTime)value).ToDTString()
					: value;
				parameters.Add(parameter);
			}

			statement = "UPDATE " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " SET " + statement.Left(statement.Length - 1) + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
			parameters.Add((new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)).CreateParameter(dbProviderFactory));

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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = @object.PrepareUpdateOrigin(attributes, dbProviderFactory).CreateCommand(connection);
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareUpdateExtent(attributes, dbProviderFactory).CreateCommand(connection);
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = @object.PrepareUpdateOrigin(attributes, dbProviderFactory).CreateCommand(connection);
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					command = @object.PrepareUpdateExtent(attributes, dbProviderFactory).CreateCommand(connection);
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

			var statement = "DELETE FROM " + definition.TableName + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
			var parameters = new List<DbParameter>()
			{
				(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID())).CreateParameter(dbProviderFactory)
			};

			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var command = (new Tuple<string, List<DbParameter>>(statement, parameters)).CreateCommand(connection);
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					statement = "DELETE FROM " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						(new KeyValuePair<string, object>("@ID", @object.GetEntityID())).CreateParameter(dbProviderFactory)
					};
					command = (new Tuple<string, List<DbParameter>>(statement, parameters)).CreateCommand(connection);
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
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var dbProviderFactory = SqlHelper.GetProviderFactory(dataSource);
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var statement = "DELETE FROM " + definition.TableName + " WHERE " + definition.PrimaryKey + "=@" + definition.PrimaryKey;
			var parameters = new List<DbParameter>()
			{
				(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID())).CreateParameter(dbProviderFactory)
			};

			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var command = (new Tuple<string, List<DbParameter>>(statement, parameters)).CreateCommand(connection);
				await command.ExecuteNonQueryAsync(cancellationToken);

				if (@object.IsGotExtendedProperties())
				{
					statement = "DELETE FROM " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						(new KeyValuePair<string, object>("@ID", @object.GetEntityID())).CreateParameter(dbProviderFactory)
					};
					command = (new Tuple<string, List<DbParameter>>(statement, parameters)).CreateCommand(connection);
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
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
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
		static Tuple<string, List<DbParameter>> PrepareSelect<T>(this DbProviderFactory dbProviderFactory, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessEntityID, definition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;
			var parentIDs = definition != null && autoAssociateWithMultipleParents && filter != null
				? filter.GetAssociatedParentIDs(definition)
				: null;
			var statementsInfo = RestrictionsHelper.PrepareSqlStatements<T>(filter, sort, businessEntityID, autoAssociateWithMultipleParents, definition, parentIDs, propertiesInfo);
			var gotAssociateWithMultipleParents = parentIDs != null && parentIDs.Count > 0;

			// listing of fields (SELECT)
			var fields = new List<string>();
			attributes = attributes != null && attributes.Count() > 0
				? attributes
				: propertiesInfo.Item1.Select(item => item.Value.Name).Concat(propertiesInfo.Item2 != null ? propertiesInfo.Item2.Select(item => item.Value.Name) : new List<string>());
			attributes.ForEach(attribute =>
			{
				var field = extendedProperties != null && extendedProperties.ContainsKey(attribute)
					? "Extent." + extendedProperties[attribute].Column + " AS " + extendedProperties[attribute].Name
					: standardProperties.ContainsKey(attribute)
						? "Origin." + (string.IsNullOrWhiteSpace(standardProperties[attribute].Column)
							? standardProperties[attribute].Name
							: standardProperties[attribute].Column + " AS " + standardProperties[attribute].Name
							)
						: null;
				if (!string.IsNullOrWhiteSpace(field))
					fields.Add(field);
			});

			// listing of table (FROM)
			var tables = definition.TableName + " AS Origin"
				+ (extendedProperties != null ? " LEFT JOIN " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " AS Extent ON Origin." + definition.PrimaryKey + "=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? " LEFT JOIN " + definition.MultipleParentAssociatesTable + " AS Link ON Origin." + definition.PrimaryKey + "=Link." + definition.MultipleParentAssociatesLinkColumn : "");

			// clauses (WHERE & ORDER BY)
			string where = statementsInfo.Item1 == null || string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? ""
				: " WHERE " + statementsInfo.Item1.Item1;
			string orderby = statementsInfo.Item2;

			// statements
			var select = "SELECT " + (gotAssociateWithMultipleParents ? "DISTINCT " : "") + string.Join(", ", fields) + " FROM " + tables + where;
			var statement = "";

			// statement of MySQL or first page
			if (!dbProviderFactory.IsGotRowNumber() || pageNumber < 2 || pageSize < 1)
			{
				statement = (pageNumber < 2 && pageSize > 0
					? "SELECT TOP " + pageSize.ToString() + " " + string.Join(", ", attributes) + " FROM (" + select + ") AS __Results"
					: select)
					+ (!string.IsNullOrWhiteSpace(orderby) ? " ORDER BY " + orderby : "");
			}

			// statement of SQL database server with pagination via ROW_NUMBER() function
			else
			{
				// normalize: add name of extended column into ORDER BY clause
				if (!string.IsNullOrWhiteSpace(orderby))
				{
					var orders = orderby.ToArray(',');
					orderby = "";
					orders.ForEach(order =>
					{
						var orderInfo = order.ToArray(' ');
						if (extendedProperties != null && extendedProperties.ContainsKey(orderInfo[0]))
							orderby += (orderby.Equals("") ? "" : ",")
								+ extendedProperties[orderInfo[0]].Column
								+ (orderInfo.Length > 1 ? " " + orderInfo[0] : "");
						else
							orderby += (orderby.Equals("") ? "" : ",") + order;
					});
				}

				statement = "SELECT " + string.Join(", ", attributes) + ","
					+ " ROW_NUMBER()" + (!string.IsNullOrWhiteSpace(orderby) ? " OVER(ORDER BY " + orderby + ")" : "") + " AS __RowNumber"
					+ " FROM (" + select + ") AS __DistinctResults";

				statement = "SELECT TOP " + pageSize.ToString() + " " + string.Join(", ", attributes)
					+ " FROM (" + statement + ") AS __Results"
					+ " WHERE __Results.__RowNumber > " + ((pageNumber - 1) * pageSize).ToString()
					+ " ORDER BY __Results.__RowNumber";
			}

			// parameters
			var parameters = statementsInfo.Item1 != null && statementsInfo.Item1.Item2 != null
				? statementsInfo.Item1.Item2.Select(param => param.CreateParameter(dbProviderFactory)).ToList()
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var info = dbProviderFactory.PrepareSelect<T>(attributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents);
				DataTable dataTable = null;

				if (dbProviderFactory.IsGotRowNumber())
				{
					var command = info.CreateCommand(connection);
					using (var reader = command.ExecuteReader())
					{
						dataTable = new DataTable();
						dataTable.Load(reader);
					}
				}
				else
				{
					var startRecord = (pageNumber - 1) * pageSize;
					if (startRecord < 0)
						startRecord = 0;

					var dataSet = new DataSet();
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = info.CreateCommand(connection);
					dataAdapter.Fill(dataSet, startRecord, pageSize, typeof(T).GetTypeName(true));
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var info = dbProviderFactory.PrepareSelect<T>(attributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents);
				DataTable dataTable = null;

				if (dbProviderFactory.IsGotRowNumber())
				{
					var command = info.CreateCommand(connection);
					using (var reader = await command.ExecuteReaderAsync())
					{
						dataTable = new DataTable();
						dataTable.Load(reader);
					}
				}
				else
				{
					var startRecord = (pageNumber - 1) * pageSize;
					if (startRecord < 0)
						startRecord = 0;

					var dataSet = new DataSet();
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = info.CreateCommand(connection);
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
			return SqlHelper.Select(context, dataSource, new List<string>() { "ID" }, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
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
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return (await SqlHelper.SelectAsync(context, dataSource, new List<string>() { "ID" }, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken))
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
				var allAttributes = standardProperties.Select(info => info.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>())
					.ToList();

				var distinctAttributes = (new List<string>(sort.GetAttributes()) { context.EntityDefinition.PrimaryKey }).Distinct().ToList();
				var otherAttributes = allAttributes.Except(distinctAttributes).ToList();

				var distinctData = context.Select<T>(dataSource, distinctAttributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents);

				var objects = distinctData
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToDictionary(@object => @object.GetEntityID<T>());

				if (otherAttributes.Count > 0)
				{
					otherAttributes.Add(context.EntityDefinition.PrimaryKey);
					var otherFilter = Filters<T>.Or(objects.Select(item => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, item.Key)));
					var otherData = context.Select<T>(dataSource, otherAttributes, otherFilter, null, 0, 1, businessEntityID, false);
					otherData.ForEach(data =>
					{
						var id = data[context.EntityDefinition.PrimaryKey] as string;
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
				var allAttributes = standardProperties.Select(info => info.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>())
					.ToList();

				var distinctAttributes = (new List<string>(sort.GetAttributes()) { context.EntityDefinition.PrimaryKey }).Distinct().ToList();
				var otherAttributes = allAttributes.Except(distinctAttributes).ToList();

				var distinctData = await context.SelectAsync<T>(dataSource, distinctAttributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken);

				var objects = distinctData
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToDictionary(@object => @object.GetEntityID<T>());

				if (otherAttributes.Count > 0)
				{
					otherAttributes.Add(context.EntityDefinition.PrimaryKey);
					var otherFilter = Filters<T>.Or(objects.Select(item => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, item.Key)));
					var otherData = await context.SelectAsync<T>(dataSource, otherAttributes, otherFilter, null, 0, 1, businessEntityID, false, cancellationToken);
					otherData.ForEach(data =>
					{
						var id = data[context.EntityDefinition.PrimaryKey] as string;
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
				: context.Find(dataSource, Filters<T>.Or(identities.Select(id => Filters<T>.Equals("ID", id))), sort, 0, 1, businessEntityID, false);
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
				: context.FindAsync(dataSource, Filters<T>.Or(identities.Select(id => Filters<T>.Equals("ID", id))), sort, 0, 1, businessEntityID, false, cancellationToken);
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

			// listing of table (FROM)
			var tables = definition.TableName + " AS Origin"
				+ (propertiesInfo.Item2 != null ? " LEFT JOIN " + definition.RepositoryDefinition.ExtendedPropertiesTableName + " AS Extent ON Origin.ID=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? " LEFT JOIN " + definition.MultipleParentAssociatesTable + " AS Link ON Origin.ID=Link." + definition.MultipleParentAssociatesLinkColumn : "");

			// clauses (WHERE)
			string where = statementsInfo.Item1 == null || string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? ""
				: " WHERE " + statementsInfo.Item1.Item1;

			// statement
			var statement = "SELECT COUNT(" + (gotAssociateWithMultipleParents ? "DISTINCT " : "") + "ID) AS Total FROM " + tables + where;

			// parameters
			var parameters = statementsInfo.Item1 != null && statementsInfo.Item1.Item2 != null
				? statementsInfo.Item1.Item2.Select(param => param.CreateParameter(dbProviderFactory)).ToList()
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				connection.Open();

				var info = dbProviderFactory.PrepareCount<T>(filter, businessEntityID, autoAssociateWithMultipleParents);
				var command = info.CreateCommand(connection);
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
			using (var connection = context.GetSqlConnection(dataSource, dbProviderFactory))
			{
				await connection.OpenAsync(cancellationToken);

				var info = dbProviderFactory.PrepareCount<T>(filter, businessEntityID, autoAssociateWithMultipleParents);
				var command = info.CreateCommand(connection);
				return (await command.ExecuteScalarAsync(cancellationToken)).CastAs<long>();
			}
		}
		#endregion

		#region Search (by query)
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