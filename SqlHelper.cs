﻿#region Related components
using System;
using System.Linq;
using System.Xml;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Reflection;

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
		public static DbProviderFactory GetProviderFactory(this DataSource dataSource)
		{
			var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? RepositoryMediator.GetConnectionStringSettings(dataSource)
				: null;

			return connectionStringSettings != null && !string.IsNullOrEmpty(connectionStringSettings.ProviderName)
				? DbProviderFactories.GetFactory(connectionStringSettings.ProviderName)
				: DbProviderFactories.GetFactory("System.Data.SqlClient");
		}

		static bool IsMicrosoftSQL(this DbProviderFactory dbProviderFactory)
		{
			return (dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "").Equals("SqlClientFactory");
		}

		static bool IsOracle(this DbProviderFactory dbProviderFactory)
		{
			return (dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "").Equals("OracleClientFactory");
		}

		static bool IsMySQL(this DbProviderFactory dbProviderFactory)
		{
			return (dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "").Equals("MySqlClientFactory");
		}

		static bool IsPostgreSQL(this DbProviderFactory dbProviderFactory)
		{
			return (dbProviderFactory != null
				? dbProviderFactory.GetType().GetTypeName(true)
				: "").Equals("NpgsqlFactory");
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
				? $" LIMIT {pageSize} OFFSET {(pageNumber - 1) * pageSize}"
				: "";
		}

		static string GetName(this DbProviderFactory dbProviderFactory)
		{
			return dbProviderFactory == null
				? "Unknown"
				: dbProviderFactory.IsMicrosoftSQL()
					? "MicrosoftSQL"
					: dbProviderFactory.IsMySQL()
						? "MySQL"
						: dbProviderFactory.IsOracle()
							? "OralceSQL"
							: dbProviderFactory.IsPostgreSQL()
								? "PostgreSQL"
								: "ODBC";
		}
		#endregion

		#region Connection
		/// <summary>
		/// Creates the connection for working with SQL database
		/// </summary>
		/// <param name="dbProviderFactory">The object that presents information of a database provider factory</param>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <returns></returns>
		public static DbConnection CreateConnection(this DbProviderFactory dbProviderFactory, DataSource dataSource)
		{		
			var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? RepositoryMediator.GetConnectionStringSettings(dataSource)
				: null;

			var connection = dbProviderFactory.CreateConnection();
			connection.ConnectionString = connectionStringSettings?.ConnectionString.Replace(StringComparison.OrdinalIgnoreCase, "{database}", dataSource.DatabaseName).Replace(StringComparison.OrdinalIgnoreCase, "{DatabaseName}", dataSource.DatabaseName);
			return connection;
		}
		#endregion

		#region Command
		static DbCommand CreateCommand(this DbConnection connection, Tuple<string, List<DbParameter>> info)
		{
			var command = connection.CreateCommand();
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter => command.Parameters.Add(parameter));
			return command;
		}

		static DbCommand CreateCommand(this DbProviderFactory dbProviderFactory, Tuple<string, List<DbParameter>> info, DbConnection connection = null)
		{
			var command = dbProviderFactory.CreateCommand();
			command.Connection = connection;
			command.CommandText = info.Item1;
			info.Item2.ForEach(parameter => command.Parameters.Add(parameter));
			return command;
		}
		#endregion

		#region DbTypes
		internal static Dictionary<Type, DbType> DbTypes = new Dictionary<Type, DbType>()
		{
			{ typeof(String), DbType.String },
			{ typeof(Char), DbType.StringFixedLength },
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
			{ typeof(Guid), DbType.Guid },
			{ typeof(DateTime), DbType.DateTime },
			{ typeof(DateTimeOffset), DbType.DateTimeOffset }
		};

		internal static DbType GetDbType(this Type type)
		{
			return SqlHelper.DbTypes[type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) ? Nullable.GetUnderlyingType(type) : type];
		}

		internal static DbType GetDbType(this AttributeInfo attribute)
		{
			return (attribute.Type.IsStringType() && (attribute.Name.EndsWith("ID") || attribute.MaxLength.Equals(32))) || attribute.IsStoredAsString()
				? DbType.AnsiStringFixedLength
				: attribute.IsStoredAsJson()
					? DbType.String
					: attribute.Type.IsEnum
						? attribute.IsEnumString()
							? DbType.String
							: DbType.Int32
					: attribute.Type.GetDbType();
		}

		internal static DbType GetDbType(this ExtendedPropertyDefinition attribute)
		{
			return attribute.Type.Equals(typeof(DateTime))
				? DbType.AnsiString
				: attribute.Type.GetDbType();
		}

		internal static string GetDbTypeString(this AttributeInfo attribute, DbProviderFactory dbProviderFactory)
		{
			return attribute.Type.IsStringType() && (attribute.Name.EndsWith("ID") || attribute.MaxLength.Equals(32))
				? typeof(String).GetDbTypeString(dbProviderFactory, 32, true, false)
				: attribute.IsStoredAsString()
					? typeof(String).GetDbTypeString(dbProviderFactory, 19, true, false)
					: attribute.IsCLOB || attribute.IsStoredAsJson()
						? typeof(String).GetDbTypeString(dbProviderFactory, 0, false, true)
						: attribute.Type.IsEnum
							? attribute.IsEnumString()
								? typeof(String).GetDbTypeString(dbProviderFactory, 50, false, false)
								: typeof(Int32).GetDbTypeString(dbProviderFactory, 0, false, false)
							: attribute.Type.GetDbTypeString(dbProviderFactory, attribute.MaxLength);
		}

		internal static string GetDbTypeString(this Type type, DbProviderFactory dbProviderFactory, int precision = 0, bool asFixedLength = false, bool asCLOB = false)
		{
			return type == null || dbProviderFactory == null
				? ""
				: type.GetDbTypeString(dbProviderFactory.GetName(), precision, asFixedLength, asCLOB);
		}

		internal static string GetDbTypeString(this Type type, string dbProviderFactoryName, int precision = 0, bool asFixedLength = false, bool asCLOB = false)
		{
			type = !type.Equals(typeof(String))
				? type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>))
					? Nullable.GetUnderlyingType(type)
					: type
				: asFixedLength
					? typeof(Char)
					: asCLOB
						? typeof(Char?)
						: type;

			precision = precision < 1 && type.Equals(typeof(String))
				? 4000
				: precision;

			var dbTypeString = !string.IsNullOrWhiteSpace(dbProviderFactoryName) && SqlHelper.DbTypeStrings.ContainsKey(type) && SqlHelper.DbTypeStrings[type].ContainsKey(dbProviderFactoryName)
				? SqlHelper.DbTypeStrings[type][dbProviderFactoryName]
				: "";

			if (dbTypeString.IndexOf("{0}") > 0 && precision > 0)
				dbTypeString = string.Format(dbTypeString, "(" + precision.ToString() + ")");

			return dbTypeString;
		}

		internal static Dictionary<Type, Dictionary<string, string>> DbTypeStrings = new Dictionary<Type, Dictionary<string, string>>()
		{
			{
				typeof(String), 
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "NVARCHAR{0}" },
					{ "MySQL", "VARCHAR{0}" },
				}
			},
			{
				typeof(Char),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "CHAR{0}" },
					{ "MySQL", "CHAR{0}" },
				}
			},
			{
				typeof(Char?),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "NTEXT" },
					{ "MySQL", "TEXT" },
				}
			},
			{
				typeof(Byte),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "TINYINT" },
					{ "MySQL", "TINYINT" },
				}
			},
			{
				typeof(SByte),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "TINYINT" },
					{ "MySQL", "TINYINT UNSIGNED" },
				}
			},
			{
				typeof(Int16),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "SMALLINT" },
					{ "MySQL", "SMALLINT" },
				}
			},
			{
				typeof(UInt16),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "SMALLINT" },
					{ "MySQL", "SMALLINT" },
				}
			},
			{
				typeof(Int32),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "INT" },
					{ "MySQL", "INT" },
				}
			},
			{
				typeof(UInt32),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "INT" },
					{ "MySQL", "INT" },
				}
			},
			{
				typeof(Int64),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "BIGINT" },
					{ "MySQL", "BIGINT" },
				}
			},
			{
				typeof(UInt64),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "BIGINT" },
					{ "MySQL", "BIGINT" },
				}
			},
			{
				typeof(Single),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "FLOAT(24)" },
					{ "MySQL", "FLOAT" },
				}
			},
			{
				typeof(Double),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "FLOAT(53)" },
					{ "MySQL", "DOUBLE" },
				}
			},
			{
				typeof(Decimal),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "DECIMAL(19,5)" },
					{ "MySQL", "NUMERIC(19,5)" },
				}
			},
			{
				typeof(Boolean),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "BIT" },
					{ "MySQL", "TINYINT(1)" },
				}
			},
			{
				typeof(DateTime),
				new Dictionary<string, string>()
				{
					{ "MicrosoftSQL", "DATETIME" },
					{ "MySQL", "DATETIME" },
				}
			}
		};
		#endregion

		#region Parameter
		static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, KeyValuePair<string, object> info)
		{
			var parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = info.Key;
			parameter.Value = info.Value;
			parameter.DbType = info.Key.EndsWith("ID") || info.Key.EndsWith("Id")
				? DbType.AnsiStringFixedLength
				: info.Value.GetType().GetDbType();
			return parameter;
		}

		static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, AttributeInfo attribute, object value)
		{
			var parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = "@" + attribute.Name;
			parameter.DbType = attribute.GetDbType();
			parameter.Value = attribute.IsStoredAsJson() 
				? value == null
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
		static T Copy<T>(this T @object, DbDataReader dataReader, Dictionary<string, AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			// create object
			@object = @object ?? ObjectService.CreateInstance<T>();

			if (@object is IBusinessEntity && extendedProperties != null &&(@object as IBusinessEntity).ExtendedProperties == null)
				(@object as IBusinessEntity).ExtendedProperties = new Dictionary<string, object>();

			// copy data
			for (var index = 0; index < dataReader.FieldCount; index++)
			{
				var name = dataReader.GetName(index);
				if (standardProperties != null && standardProperties.ContainsKey(name))
				{
					var attribute = standardProperties[name];
					var value = dataReader[index];
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
						else if (attribute.Type.IsEnum)
							value = attribute.IsEnumString()
								? value.ToString().ToEnum(attribute.Type)
								: value.CastAs<int>();
					}
					@object.SetAttributeValue(attribute, value, true);
				}
				else if (extendedProperties != null && extendedProperties.ContainsKey(name))
				{
					var attribute = extendedProperties[name];
					var value = dataReader[index];
					if (value != null && attribute.Type.IsDateTimeType())
						value = DateTime.Parse(value as string);
					(@object as IBusinessEntity).ExtendedProperties[attribute.Name] = value.CastAs(attribute.Type);
				}
			}

			// return object
			return @object;
		}
		#endregion

		#region Copy (DataRow)
		static T Copy<T>(this T @object, DataRow dataRow, Dictionary<string, AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			@object = @object ?? ObjectService.CreateInstance<T>();

			if (@object is IBusinessEntity && extendedProperties != null && (@object as IBusinessEntity).ExtendedProperties == null)
				(@object as IBusinessEntity).ExtendedProperties = new Dictionary<string, object>();

			for (var index = 0; index < dataRow.Table.Columns.Count; index++)
			{
				var name = dataRow.Table.Columns[index].ColumnName;
				if (standardProperties != null && standardProperties.ContainsKey(name))
				{
					var attribute = standardProperties[name];
					var value = dataRow[name];
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
						else if (attribute.Type.IsEnum)
							value = attribute.IsEnumString()
								? value.ToString().ToEnum(attribute.Type)
								: value.CastAs<int>();
					}
					@object.SetAttributeValue(attribute, value, true);
				}
				else if (extendedProperties != null && extendedProperties.ContainsKey(name))
				{
					var attribute = extendedProperties[name];
					var value = dataRow[name];
					if (value != null && attribute.Type.IsDateTimeType())
						value = DateTime.Parse(value as string);
					(@object as IBusinessEntity).ExtendedProperties[attribute.Name] = value.CastAs(attribute.Type);
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

			var statement = $"INSERT INTO {definition.TableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareCreateExtent<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = "ID,SystemID,RepositoryID,EntityID".ToList();
			var values = columns.Select(c => "@" + c).ToList();
			var parameters = new List<DbParameter>()
			{
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@SystemID", (@object as IBusinessEntity).SystemID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@RepositoryID", (@object as IBusinessEntity).RepositoryID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@EntityID", (@object as IBusinessEntity).EntityID))
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

			var statement = $"INSERT INTO {definition.RepositoryDefinition.ExtendedPropertiesTableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";

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
			if (@object == null)
				throw new NullReferenceException("Cannot create new because the object is null");

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
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
			if (@object == null)
				throw new NullReferenceException("Cannot create new because the object is null");

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var command = connection.CreateCommand(@object.PrepareCreateOrigin(dbProviderFactory));
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareCreateExtent(dbProviderFactory));
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}
		#endregion

		#region Get
		static Tuple<string, List<DbParameter>> PrepareGetOrigin<T>(this T @object, string id, DbProviderFactory dbProviderFactory) where T : class
		{
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var fields = definition.Attributes
				.Where(attribute => !attribute.IsIgnoredIfNull() || (attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) != null))
				.Select(attribute => "Origin." + (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column + " AS " + attribute.Name))
				.ToList();

			var info = Filters<T>.Equals(definition.PrimaryKey, id).GetSqlStatement();
			var statement = $"SELECT {string.Join(", ", fields)} FROM {definition.TableName} AS Origin WHERE {info.Item1}";
			var parameters = info.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList();

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareGetExtent<T>(this T @object, string id, DbProviderFactory dbProviderFactory, List<ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			var fields = extendedProperties.Select(attribute => "Origin." + attribute.Column + " AS " + attribute.Name)
				.Concat(new List<string>() { "Origin.ID" })
				.ToList();

			var info = Filters<T>.Equals("ID", id).GetSqlStatement();
			var statement = $"SELECT {string.Join(", ", fields)} FROM {RepositoryMediator.GetEntityDefinition<T>().RepositoryDefinition.ExtendedPropertiesTableName} AS Origin WHERE {info.Item1}";
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
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				connection.Open();

				var command = connection.CreateCommand(@object.PrepareGetOrigin<T>(id, dbProviderFactory));
				using (var dataReader = command.ExecuteReader())
				{
					if (dataReader.Read())
						@object = @object.Copy<T>(dataReader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = connection.CreateCommand(@object.PrepareGetExtent<T>(id, dbProviderFactory, extendedProperties));
					using (var dataReader = command.ExecuteReader())
					{
						if (dataReader.Read())
							@object = @object.Copy<T>(dataReader, null, extendedProperties.ToDictionary(attribute => attribute.Name));
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
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var command = connection.CreateCommand(@object.PrepareGetOrigin<T>(id, dbProviderFactory));
				using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
				{
					if (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
						@object = @object.Copy<T>(dataReader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = context.EntityDefinition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions;
					command = connection.CreateCommand(@object.PrepareGetExtent<T>(id, dbProviderFactory, extendedProperties));
					using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
					{
						if (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
							@object = @object.Copy<T>(dataReader, null, extendedProperties.ToDictionary(attribute => attribute.Name));
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
			var objects = await context.FindAsync(dataSource, filter, sort, 1, 1, businessEntityID, false, cancellationToken).ConfigureAwait(false);
			return objects != null && objects.Count > 0
				? objects[0]
				: null;
		}
		#endregion

		#region Get (by definition and identity)
		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <returns></returns>
		public static object Get(EntityDefinition definition, string id)
		{
			var @object = definition.Type.CreateInstance();

			var dataSource = definition.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				connection.Open();

				var standardProperties = definition.Attributes
					.Where(attribute => !attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) != null)
					.ToDictionary(attribute => attribute.Name);

				var fields = standardProperties.Select(info => "Origin." + (string.IsNullOrEmpty(info.Value.Column) ? info.Value.Name : info.Value.Column + " AS " + info.Value.Name));

				var command = connection.CreateCommand();
				command.CommandText = $"SELECT {string.Join(", ", fields)} FROM {definition.TableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'";

				using (var dataReader = command.ExecuteReader())
				{
					if (dataReader.Read())
						@object.Copy(dataReader, standardProperties, null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name);
					fields = extendedProperties.Select(info => $"Origin.{info.Value.Column} AS {info.Value.Name}");

					command = connection.CreateCommand();
					command.CommandText = $"SELECT {string.Join(", ", fields)} FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'";

					using (var dataReader = command.ExecuteReader())
					{
						if (dataReader.Read())
							@object.Copy(dataReader, null, extendedProperties);
					}
				}
			}

			return @object;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<object> GetAsync(EntityDefinition definition, string id, CancellationToken cancellationToken = default(CancellationToken))
		{
			var @object = definition.Type.CreateInstance();

			var dataSource = definition.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var standardProperties = definition.Attributes
					.Where(attribute => !attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) != null)
					.ToDictionary(attribute => attribute.Name);

				var fields = standardProperties.Select(info => "Origin." + (string.IsNullOrEmpty(info.Value.Column) ? info.Value.Name : info.Value.Column + " AS " + info.Value.Name));

				var command = connection.CreateCommand();
				command.CommandText = $"SELECT {string.Join(", ", fields)} FROM {definition.TableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'";

				using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
				{
					if (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
						@object.Copy(dataReader, standardProperties, null);
				}

				if (@object.IsGotExtendedProperties())
				{
					var extendedProperties = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name);
					fields = extendedProperties.Select(info => $"Origin.{info.Value.Column} AS {info.Value.Name}");

					command = connection.CreateCommand();
					command.CommandText = $"SELECT {string.Join(", ", fields)} FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'";

					using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
					{
						if (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
							@object.Copy(dataReader, null, extendedProperties);
					}
				}
			}

			return @object;
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

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID(definition.PrimaryKey))));
			var statement = $"UPDATE {definition.TableName} SET {string.Join(", ", columns)} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}";

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

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)));
			var statement = $"UPDATE {definition.RepositoryDefinition.ExtendedPropertiesTableName} SET {string.Join(", ", columns)} WHERE ID=@ID";

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
			if (@object == null)
				throw new NullReferenceException("Cannot replace because the object is null");

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
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
			if (@object == null)
				throw new NullReferenceException("Cannot replace because the object is null");

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var command = connection.CreateCommand(@object.PrepareReplaceOrigin(dbProviderFactory));
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareReplaceExtent(dbProviderFactory));
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
			var standardProperties = definition.Attributes.ToDictionary(attribute => attribute.Name.ToLower());
			foreach (var attribute in attributes)
			{
				if (!standardProperties.ContainsKey(attribute.ToLower()))
					continue;

				var value = @object.GetAttributeValue(standardProperties[attribute.ToLower()].Name);
				if (value == null && standardProperties[attribute.ToLower()].IsIgnoredIfNull())
					continue;

				columns.Add((string.IsNullOrEmpty(standardProperties[attribute.ToLower()].Column) ? standardProperties[attribute.ToLower()].Name : standardProperties[attribute.ToLower()].Column) + "=" + "@" + standardProperties[attribute.ToLower()].Name);
				parameters.Add(dbProviderFactory.CreateParameter(standardProperties[attribute.ToLower()], value));
			}

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID(definition.PrimaryKey))));
			var statement = $"UPDATE {definition.TableName} SET {string.Join(", ", columns)} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}";

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareUpdateExtent<T>(this T @object, List<string> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var colums = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var extendedProperties = definition.RuntimeEntities[(@object as IBusinessEntity).EntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name.ToLower());
			foreach (var attribute in attributes)
			{
				if (!extendedProperties.ContainsKey(attribute.ToLower()))
					continue;

				colums.Add(extendedProperties[attribute.ToLower()].Column + "=@" + extendedProperties[attribute.ToLower()].Name);

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(extendedProperties[attribute.ToLower()].Name)
					? (@object as IBusinessEntity).ExtendedProperties[extendedProperties[attribute.ToLower()].Name]
					: extendedProperties[attribute.ToLower()].GetDefaultValue();
				parameters.Add(dbProviderFactory.CreateParameter(extendedProperties[attribute.ToLower()], value));
			}

			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)));
			var statement = $"UPDATE {definition.RepositoryDefinition.ExtendedPropertiesTableName} SET {string.Join(", ", colums)} WHERE ID=@ID";

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
			if (@object == null)
				throw new NullReferenceException("Cannot update because the object is null");

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
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
			if (@object == null)
				throw new NullReferenceException("Cannot update because the object is null");

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var command = connection.CreateCommand(@object.PrepareUpdateOrigin(attributes, dbProviderFactory));
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				if (@object.IsGotExtendedProperties())
				{
					command = connection.CreateCommand(@object.PrepareUpdateExtent(attributes, dbProviderFactory));
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
			var dbProviderFactory = dataSource.GetProviderFactory();
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				connection.Open();

				var statement = $"DELETE FROM {definition.TableName} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}";
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID(definition.PrimaryKey)))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				command.ExecuteNonQuery();

				if (@object.IsGotExtendedProperties())
				{
					statement = $"DELETE FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID))
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
			var dbProviderFactory = dataSource.GetProviderFactory();
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var statement = $"DELETE FROM {definition.TableName} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}";
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID(definition.PrimaryKey)))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				if (@object.IsGotExtendedProperties())
				{
					statement = $"DELETE FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID))
					};
					command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

			var dbProviderFactory = dataSource.GetProviderFactory();
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				connection.Open();

				var statement = $"DELETE FROM {definition.TableName} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}";
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, id))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				command.ExecuteNonQuery();

				if (definition.Extendable && definition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(definition.RepositoryDefinition.ExtendedPropertiesTableName))
				{
					statement = $"DELETE FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID";
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

			var dbProviderFactory = dataSource.GetProviderFactory();
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var statement = $"DELETE FROM {definition.TableName} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}";
				var parameters = new List<DbParameter>()
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, id))
				};
				var command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

				if (definition.Extendable && definition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(definition.RepositoryDefinition.ExtendedPropertiesTableName))
				{
					statement = $"DELETE FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID";
					parameters = new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", id))
					};
					command = connection.CreateCommand((new Tuple<string, List<DbParameter>>(statement, parameters)));
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

			var statementsInfo = Extensions.PrepareSqlStatements<T>(filter, sort, businessEntityID, autoAssociateWithMultipleParents, definition, parentIDs, propertiesInfo);

			// fields/columns (SELECT)
			var fields = (attributes != null && attributes.Count() > 0
					? attributes
					: standardProperties
						.Select(item => item.Value.Name)
						.Concat(extendedProperties != null ? extendedProperties.Select(item => item.Value.Name) : new List<string>())
				)
				.Where(attribute => standardProperties.ContainsKey(attribute.ToLower()) || (extendedProperties != null && extendedProperties.ContainsKey(attribute.ToLower())))
				.ToList();

			var columns = fields.Select(field =>
				extendedProperties != null && extendedProperties.ContainsKey(field.ToLower())
					? "Extent." + extendedProperties[field.ToLower()].Column + " AS " + extendedProperties[field.ToLower()].Name
					: "Origin." + (string.IsNullOrWhiteSpace(standardProperties[field.ToLower()].Column)
						? standardProperties[field.ToLower()].Name
						: standardProperties[field.ToLower()].Column + " AS " + standardProperties[field.ToLower()].Name)
				)
				.ToList();

			// tables (FROM)
			var tables = $" FROM {definition.TableName} AS Origin"
				+ (extendedProperties != null ? $" LEFT JOIN {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Extent ON Origin.{definition.PrimaryKey}=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? $" LEFT JOIN {definition.MultipleParentAssociatesTable} AS Link ON Origin.{definition.PrimaryKey}=Link.{definition.MultipleParentAssociatesLinkColumn}" : "");

			// filtering expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? $" WHERE {statementsInfo.Item1.Item1}"
				: "";

			// ordering expressions (ORDER BY)
			string orderby = statementsInfo.Item2;

			// statements
			var select = $"SELECT {(gotAssociateWithMultipleParents ? "DISTINCT " : "")}" + string.Join(", ", columns) + tables + where;
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
				statement = $"SELECT {string.Join(", ", fields)},"
					+ $" ROW_NUMBER() OVER(ORDER BY {(!string.IsNullOrWhiteSpace(orderby) ? orderby : definition.PrimaryKey + " ASC")}) AS __RowNumber"
					+ $" FROM ({select}) AS __Records";

				statement = $"SELECT {string.Join(", ", fields)} FROM ({statement}) AS __Results"
					+ $" WHERE __Results.__RowNumber > {(pageNumber - 1) * pageSize} AND __Results.__RowNumber <= {pageNumber * pageSize}"
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

		static DataTable CreateDataTable(this DbDataReader dataReader, string name = "Table", bool doLoad = false)
		{
			var dataTable = new DataTable(name);

			if (doLoad)
				dataTable.Load(dataReader);

			else
				foreach (DataRow info in dataReader.GetSchemaTable().Rows)
				{
					var dataColumn = new DataColumn();
					dataColumn.ColumnName = info["ColumnName"].ToString();
					dataColumn.Unique = Convert.ToBoolean(info["IsUnique"]);
					dataColumn.AllowDBNull = Convert.ToBoolean(info["AllowDBNull"]);
					dataColumn.ReadOnly = Convert.ToBoolean(info["IsReadOnly"]);
					dataColumn.DataType = (Type)info["DataType"];
					dataTable.Columns.Add(dataColumn);
				}

			return dataTable;
		}

		static void Append(this DataTable dataTable, DbDataReader dataReader)
		{
			object[] data = new object[dataReader.FieldCount];
			for (int index = 0; index < dataReader.FieldCount; index++)
				data[index] = dataReader[index];
			dataTable.LoadDataRow(data, true);
		}

		/// <summary>
		/// Finds all the matched records and return the collection of <see cref="DataRow">DataRow</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include all attributes)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static List<DataRow> Select<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				connection.Open();

				DataTable dataTable = null;
				var info = dbProviderFactory.PrepareSelect<T>(attributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var dataReader = command.ExecuteReader())
					{
						dataTable = dataReader.CreateDataTable(typeof(T).GetTypeName(true));
						while (dataReader.Read())
							dataTable.Append(dataReader);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
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
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include all attributes)</param>
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
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				DataTable dataTable = null;
				var info = dbProviderFactory.PrepareSelect<T>(attributes, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var dataReader = await command.ExecuteReaderAsync().ConfigureAwait(false))
					{
						dataTable = dataReader.CreateDataTable(typeof(T).GetTypeName(true));
						while (await dataReader.ReadAsync().ConfigureAwait(false))
							dataTable.Append(dataReader);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

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
			return (await SqlHelper.SelectAsync(context, dataSource, new List<string>() { context.EntityDefinition.PrimaryKey }, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false))
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
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>());

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
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>());

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
				return (await context.SelectAsync<T>(dataSource, null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false))
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

			var statementsInfo = Extensions.PrepareSqlStatements<T>(filter, null, businessEntityID, autoAssociateWithMultipleParents, definition, parentIDs, propertiesInfo);

			// tables (FROM)
			var tables = $" FROM {definition.TableName} AS Origin"
				+ (propertiesInfo.Item2 != null ? $" LEFT JOIN {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Extent ON Origin.{definition.PrimaryKey}=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? $" LEFT JOIN {definition.MultipleParentAssociatesTable} AS Link ON Origin.{definition.PrimaryKey}=Link.{definition.MultipleParentAssociatesLinkColumn}" : "");

			// couting expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// statement
			var statement = $"SELECT COUNT({(gotAssociateWithMultipleParents ? "DISTINCT " : "")}{definition.PrimaryKey}) AS TotalRecords" + tables + where;

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
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
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
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var command = connection.CreateCommand(dbProviderFactory.PrepareCount<T>(filter, businessEntityID, autoAssociateWithMultipleParents));
				return (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>();
			}
		}
		#endregion

		#region Search
		/// <summary>
		/// Gets the terms for searching in SQL database using full-text search
		/// </summary>
		/// <param name="dbProviderFactory"></param>
		/// <param name="searchQuery"></param>
		/// <returns></returns>
		public static string GetSearchTerms(this DbProviderFactory dbProviderFactory, SearchQuery searchQuery)
		{
			var searchTerms = "";

			// Microsoft SQL Server
			if (dbProviderFactory.IsMicrosoftSQL())
			{
				// prepare AND/OR/AND NOT terms
				var andSearchTerms = "";
				searchQuery.AndWords.ForEach(word => andSearchTerms += "\"*" + word + "*\"" + " AND ");
				searchQuery.AndPhrases.ForEach(phrase => andSearchTerms += "\"" + phrase + "\" AND ");
				if (!andSearchTerms.Equals(""))
					andSearchTerms = andSearchTerms.Left(andSearchTerms.Length - 5);

				var notSearchTerms = "";
				searchQuery.NotWords.ForEach(word => notSearchTerms += " AND NOT " + word);
				searchQuery.NotPhrases.ForEach(phrase => notSearchTerms += " AND NOT \"" + phrase + "\"");
				if (!notSearchTerms.Equals(""))
					notSearchTerms = notSearchTerms.Trim();

				var orSearchTerms = "";
				searchQuery.OrWords.ForEach(word => orSearchTerms += "\"*" + word + "*\"" + " OR ");
				searchQuery.OrPhrases.ForEach(phrase => orSearchTerms += "\"" + phrase + "\" OR ");
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

			// MySQL
			else if (dbProviderFactory.IsMySQL())
			{
				searchQuery.AndWords.ForEach(word => searchTerms += (searchTerms.Equals("") ? "" : " ") + "+" + word);
				searchQuery.AndPhrases.ForEach(phrase => searchTerms += (searchTerms.Equals("") ? "" : " ") + "+\"" + phrase + "\"");

				searchQuery.NotWords.ForEach(word => searchTerms += (searchTerms.Equals("") ? "" : " ") + "-" + word);
				searchQuery.NotPhrases.ForEach(phrase => searchTerms += (searchTerms.Equals("") ? "" : " ") + "-\"" + phrase + "\"");

				searchQuery.OrWords.ForEach(word => searchTerms += (searchTerms.Equals("") ? "" : " ") + word);
				searchQuery.OrPhrases.ForEach(phrase => searchTerms += (searchTerms.Equals("") ? "" : " ") + "\"" + phrase + "\"");
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

			var statementsInfo = Extensions.PrepareSqlStatements<T>(filter, null, businessEntityID, false, definition, null, propertiesInfo);

			// fields/columns (SELECT)
			var fields = (attributes != null && attributes.Count() > 0
					? attributes
					: standardProperties
						.Select(item => item.Value.Name)
						.Concat(extendedProperties != null ? extendedProperties.Select(item => item.Value.Name) : new List<string>())
				)
				.Where(attribute => standardProperties.ContainsKey(attribute.ToLower()) || (extendedProperties != null && extendedProperties.ContainsKey(attribute.ToLower())))
				.ToList();

			var columns = fields.Select(field =>
				extendedProperties != null && extendedProperties.ContainsKey(field.ToLower())
					? "Extent." + extendedProperties[field.ToLower()].Column + " AS " + extendedProperties[field.ToLower()].Name
					: "Origin." + (string.IsNullOrWhiteSpace(standardProperties[field.ToLower()].Column)
						? standardProperties[field.ToLower()].Name
						: standardProperties[field.ToLower()].Column + " AS " + standardProperties[field.ToLower()].Name)
				)
				.ToList();

			// tables (FROM)
			var tables = $" FROM {definition.TableName} AS Origin"
				+ (extendedProperties != null ? $" LEFT JOIN {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Extent ON Origin.{definition.PrimaryKey}=Extent.ID" : "");

			// filtering expressions (WHERE)
			var where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// ordering expressions (ORDER BY)
			var orderby = "";

			// searching terms
			var searchTerms = dbProviderFactory.GetSearchTerms(new SearchQuery(query));

			// Microsoft SQL Server
			if (dbProviderFactory.IsMicrosoftSQL())
			{
				fields.Add("SearchScore");
				columns.Add("Search.[RANK] AS SearchScore");
				tables += $" INNER JOIN CONTAINSTABLE ({definition.TableName}, {searchInColumns}, {searchTerms}) AS Search ON Origin.{definition.PrimaryKey}=Search.[KEY]";
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
				columns.Add($"(MATCH({searchInColumns}) AGAINST ({searchTerms} IN BOOLEAN MODE) AS SearchScore");
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
				statement = $"SELECT {string.Join(", ", fields)}, ROW_NUMBER() OVER(ORDER BY {(!string.IsNullOrWhiteSpace(orderby) ? orderby : definition.PrimaryKey + " ASC")}) AS __RowNumber"
					+ $" FROM ({select}) AS __Records";

				statement = $"SELECT {string.Join(", ", fields)} FROM ({statement}) AS __Results"
					+ $" WHERE __Results.__RowNumber > {(pageNumber - 1) * pageSize} AND __Results.__RowNumber <= {pageNumber * pageSize}"
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

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				connection.Open();

				var objects = new List<T>();
				var info = dbProviderFactory.PrepareSearch<T>(null, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var dataReader = command.ExecuteReader())
					{
						while (dataReader.Read())
							objects.Add(ObjectService.CreateInstance<T>().Copy(dataReader, standardProperties, extendedProperties));
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					objects = dataSet.Tables[0].Rows.ToList()
						.Select(dataRow => ObjectService.CreateInstance<T>().Copy(dataRow, standardProperties, extendedProperties))
						.ToList();
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

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var objects = new List<T>();
				var info = dbProviderFactory.PrepareSearch<T>(null, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
					{
						while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
							objects.Add(ObjectService.CreateInstance<T>().Copy(dataReader, standardProperties, extendedProperties));
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					objects = dataSet.Tables[0].Rows.ToList()
						.Select(dataRow => ObjectService.CreateInstance<T>().Copy(dataRow, standardProperties, extendedProperties))
						.ToList();
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

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				connection.Open();

				var identities = new List<string>();
				var info = dbProviderFactory.PrepareSearch<T>(new List<string>() { context.EntityDefinition.PrimaryKey }, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var dataReader = command.ExecuteReader())
					{
						while (dataReader.Read())
							identities.Add(dataReader[context.EntityDefinition.PrimaryKey].CastAs<string>());
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter();
					dataAdapter.SelectCommand = connection.CreateCommand(info);

					var dataSet = new DataSet();
					if (pageSize > 0)
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					identities = dataSet.Tables[0].Rows.ToList()
						.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
						.ToList();
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

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var identities = new List<string>();
				var info = dbProviderFactory.PrepareSearch<T>(new List<string>() { context.EntityDefinition.PrimaryKey }, query, filter, pageSize, pageNumber, businessEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset())
				{
					var command = connection.CreateCommand(info);
					using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
					{
						while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
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
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
					else
						dataAdapter.Fill(dataSet, typeof(T).GetTypeName(true));

					identities = dataSet.Tables[0].Rows.ToList()
						.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
						.ToList();
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

			var statementsInfo = Extensions.PrepareSqlStatements<T>(filter, null, businessEntityID, false, definition, null, propertiesInfo);

			// tables (FROM)
			var tables = $" FROM {definition.TableName} AS Origin"
				+ (extendedProperties != null ? $" LEFT JOIN {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Extent ON Origin.{definition.PrimaryKey}=Extent.ID" : "");

			// filtering expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// searching terms
			var searchTerms = dbProviderFactory.GetSearchTerms(new SearchQuery(query));

			// Microsoft SQL Server
			if (dbProviderFactory.IsMicrosoftSQL())
				tables += $" INNER JOIN CONTAINSTABLE ({definition.TableName}, {searchInColumns}, {searchTerms}) AS Search ON Origin.{definition.PrimaryKey}=Search.[KEY]";

			// MySQL
			else if (dbProviderFactory.IsMySQL())
			{
				searchInColumns = !searchInColumns.Equals("*")
					? searchInColumns
					: standardProperties
						.Where(attribute => attribute.Value.IsSearchable())
						.Select(attribute => $"Origin.{attribute.Value.Name}")
						.ToList()
						.ToString(",");
				where += (!where.Equals("") ? " AND " : " WHERE ")
					+ $"(MATCH({searchInColumns}) AGAINST ({searchTerms} IN BOOLEAN MODE) > 0";
			}

			// statement
			var statement = $"SELECT COUNT(Origin.{definition.PrimaryKey}) AS TotalRecords" + tables + where;

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
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
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
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
				var command = connection.CreateCommand(dbProviderFactory.PrepareCount<T>(query, filter, businessEntityID));
				return (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>();
			}
		}
		#endregion

		#region Schemas & Indexes
		internal static async Task CreateTableAsync(this RepositoryContext context, DataSource dataSource)
		{
			// prepare
			var dbProviderFactory = dataSource.GetProviderFactory();
			var sql = "";
			switch (dbProviderFactory.GetName())
			{
				case "MicrosoftSQL":
					sql = $"CREATE TABLE [{context.EntityDefinition.TableName}] ("
						+ string.Join(", ", context.EntityDefinition.Attributes.Select(attribute => "[" + (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + "] " + attribute.GetDbTypeString(dbProviderFactory) + " " + (attribute.NotNull ? "NOT " : "") + "NULL"))
						+ $", CONSTRAINT [PK_{context.EntityDefinition.TableName}] PRIMARY KEY CLUSTERED ([{context.EntityDefinition.PrimaryKey}] ASC) "
						+ "WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, IGNORE_DUP_KEY=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=ON) ON [PRIMARY]) ON [PRIMARY]";
					break;

				case "MySQL":
				case "PostgreSQL":
					sql = $"CREATE TABLE {context.EntityDefinition.TableName} ("
						+ string.Join(", ", context.EntityDefinition.Attributes.Select(attribute => (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + " " + attribute.GetDbTypeString(dbProviderFactory) + " " + (attribute.NotNull ? "NOT " : "") + "NULL"))
						+ $", PRIMARY KEY ({context.EntityDefinition.PrimaryKey}))";
					break;
			}

			// create table
			if (!sql.Equals(""))
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					await connection.OpenAsync().ConfigureAwait(false);
					try
					{
						var command = connection.CreateCommand();
						command.CommandText = sql;
						await command.ExecuteNonQueryAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException("Error occurred while creating new table [" + sql + "]", ex);
					}
				}
		}

		internal static async Task CreateTableIndexesAsync(this RepositoryContext context, DataSource dataSource)
		{
			// prepare
			var prefix = "IDX_" + context.EntityDefinition.TableName;
			var indexes = new Dictionary<string, List<AttributeInfo>>()
			{
				{ prefix, new List<AttributeInfo>() }
			};
			var uniqueIndexes = new Dictionary<string, List<AttributeInfo>>();

			context.EntityDefinition.Attributes.ForEach(attribute =>
			{
				var attributes = attribute.Info.GetCustomAttributes(typeof(SortableAttribute), true);
				if (attributes.Length > 0)
				{
					var attr = attributes[0] as SortableAttribute;
					if (!string.IsNullOrWhiteSpace(attr.UniqueIndexName))
					{
						var name = prefix + "_" + attr.UniqueIndexName;
						if (!uniqueIndexes.ContainsKey(name))
							uniqueIndexes.Add(name, new List<AttributeInfo>());
						uniqueIndexes[name].Add(attribute);

						if (!string.IsNullOrWhiteSpace(attr.IndexName))
						{
							name = prefix + "_" + attr.IndexName;
							if (!indexes.ContainsKey(name))
								indexes.Add(name, new List<AttributeInfo>());
							indexes[name].Add(attribute);
						}
					}
					else
					{
						var name = prefix + (string.IsNullOrWhiteSpace(attr.IndexName) ? "" : "_" + attr.IndexName);
						if (!indexes.ContainsKey(name))
							indexes.Add(name, new List<AttributeInfo>());
						indexes[name].Add(attribute);
					}
				}
			});

			var dbProviderFactory = dataSource.GetProviderFactory();
			var sql = "";
			switch (dbProviderFactory.GetName())
			{
				case "MicrosoftSQL":
					indexes.ForEach(info =>
					{
						if (info.Value.Count > 0)
							sql += (sql.Equals("") ? "" : ";")
								+ $"CREATE NONCLUSTERED INDEX [{info.Key}] ON [{context.EntityDefinition.TableName}] ("
								+ string.Join(", ", info.Value.Select(attribute => "[" + (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + "] ASC"))
								+ ") WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY]";
					});
					uniqueIndexes.ForEach(info =>
					{
						if (info.Value.Count > 0)
							sql += (sql.Equals("") ? "" : ";")
								+ $"CREATE UNIQUE NONCLUSTERED INDEX [{info.Key}] ON [{context.EntityDefinition.TableName}] ("
								+ string.Join(", ", info.Value.Select(attribute => "[" + (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + "] ASC"))
								+ ") WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY]";
					});
					break;

				case "MySQL":
				case "PostgreSQL":
					indexes.ForEach(info =>
					{
						if (info.Value.Count > 0)
							sql += (sql.Equals("") ? "" : ";\n")
								+ $"CREATE INDEX {info.Key} ON {context.EntityDefinition.TableName} ("
								+ string.Join(", ", info.Value.Select(attribute => (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + " ASC"))
								+ ")";
					});
					uniqueIndexes.ForEach(info =>
					{
						if (info.Value.Count > 0)
							sql += (sql.Equals("") ? "" : ";\n")
								+ $"CREATE UNIQUE INDEX {info.Key} ON {context.EntityDefinition.TableName} ("
								+ string.Join(", ", info.Value.Select(attribute => (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + " ASC"))
								+ ")";
					});
					break;
			}

			// create index
			if (!sql.Equals(""))
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					await connection.OpenAsync().ConfigureAwait(false);
					try
					{
						var command = connection.CreateCommand();
						command.CommandText = sql;
						await command.ExecuteNonQueryAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException("Error occurred while creating new indexes [" + sql + "]", ex);
					}
				}
		}

		internal static async Task CreateTableFulltextIndexAsync(this RepositoryContext context, DataSource dataSource)
		{
			// prepare
			var columns = context.EntityDefinition.Searchable
				? context.EntityDefinition.Attributes
					.Where(attribute => attribute.Info.GetCustomAttributes(typeof(SearchableAttribute), true).Length > 0)
					.Select(attribute => string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column)
					.ToList()
				: new List<string>();

			var dbProviderFactory = dataSource.GetProviderFactory();
			var sql = "";

			// check to create default fulltext catalog on Microsoft SQL Server
			if (dbProviderFactory.GetName() == "MicrosoftSQL")
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					await connection.OpenAsync().ConfigureAwait(false);
					try
					{						
						var command = connection.CreateCommand();
						command.CommandText = $"SELECT COUNT(name) FROM sys.fulltext_catalogs WHERE name='{connection.Database.Replace("'", "")}'";
						if ((await command.ExecuteScalarAsync().ConfigureAwait(false)).CastAs<int>() < 1)
						{
							command.CommandText = $"CREATE FULLTEXT CATALOG [{connection.Database.Replace("'", "")}] WITH ACCENT_SENSITIVITY = OFF AS DEFAULT AUTHORIZATION [dbo]";
							await command.ExecuteNonQueryAsync().ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException("Error occurred while creating the default full-text catalog of Microsoft SQL Server [" + sql + "]", ex);
					}
				}

			if (columns.Count > 0)
				switch (dbProviderFactory.GetName())
				{
					case "MicrosoftSQL":
						sql = $"CREATE FULLTEXT INDEX ON [{context.EntityDefinition.TableName}] ({string.Join(", ", columns)}) KEY INDEX [PK_{context.EntityDefinition.TableName}]";
						break;

					case "MySQL":
					case "PostgreSQL":
						sql = $"CREATE FULLTEXT INDEX FT_{context.EntityDefinition.TableName} ON {context.EntityDefinition.TableName} ({string.Join(", ", columns)})";
						break;
				}

			// create full-text index
			if (!sql.Equals(""))
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					await connection.OpenAsync().ConfigureAwait(false);
					try
					{
						var command = connection.CreateCommand();
						command.CommandText = sql;
						await command.ExecuteNonQueryAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException("Error occurred while creating new full-text indexes [" + sql + "]", ex);
					}
				}
		}

		internal static async Task CreateMapTableAsync(this RepositoryContext context, DataSource dataSource)
		{
			// prepare
			var columns = context.EntityDefinition.Attributes
				.Where(attribute => attribute.Name.Equals(context.EntityDefinition.ParentAssociatedProperty) || attribute.Name.Equals(context.EntityDefinition.PrimaryKey))
				.ToDictionary(attribute => attribute.Name.Equals(context.EntityDefinition.PrimaryKey) ? context.EntityDefinition.MultipleParentAssociatesLinkColumn : context.EntityDefinition.MultipleParentAssociatesMapColumn);
			var dbProviderFactory = dataSource.GetProviderFactory();
			var sql = "";
			switch (dbProviderFactory.GetName())
			{
				case "MicrosoftSQL":
					sql = $"CREATE TABLE [{context.EntityDefinition.MultipleParentAssociatesTable}] ("
						+ string.Join(", ", columns.Select(info => "[" + info.Key + "] " + info.Value.GetDbTypeString(dbProviderFactory) + " NOT  NULL"))
						+ $", CONSTRAINT [PK_{context.EntityDefinition.MultipleParentAssociatesTable}] PRIMARY KEY CLUSTERED ({string.Join(", ", columns.Select(info => $"[{info.Key}] ASC"))})"
						+ " WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, IGNORE_DUP_KEY=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=ON) ON [PRIMARY]) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]";
					break;

				case "MySQL":
				case "PostgreSQL":
					sql = $"CREATE TABLE {context.EntityDefinition.MultipleParentAssociatesTable} ("
						+ string.Join(", ", columns.Select(info => $"{info.Key} {info.Value.GetDbTypeString(dbProviderFactory)} NOT  NULL"))
						+ $", PRIMARY KEY ({string.Join(", ", columns.Select(info => $"{info.Key} ASC"))})"
						+ ")";
					break;
			}

			// create table
			if (!sql.Equals(""))
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					await connection.OpenAsync().ConfigureAwait(false);
					try
					{
						var command = connection.CreateCommand();
						command.CommandText = sql;
						await command.ExecuteNonQueryAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException("Error occurred while creating new table [" + sql + "]", ex);
					}
				}
		}

		internal static async Task CreateExtentTableAsync(this RepositoryContext context, DataSource dataSource)
		{
			var dbProviderFactory = dataSource.GetProviderFactory();
			var dbProviderFactoryName = dbProviderFactory.GetName();
			var tableName = context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName;

			var columns = new Dictionary<string, Tuple<Type, int>>()
			{
				{ "ID", new Tuple<Type, int>(typeof(String), 32) },
				{ "SystemID", new Tuple<Type, int>(typeof(String), 32) },
				{ "RepositoryID", new Tuple<Type, int>(typeof(String), 32) },
				{ "EntityID", new Tuple<Type, int>(typeof(String), 32) },
			};

			var max = dbProviderFactoryName.Equals("MySQL") ? 15 : 30;
			for (var index = 1; index <= max; index++)
				columns.Add("SmallText" + index.ToString(), new Tuple<Type, int>(typeof(String), 250));

			max = dbProviderFactoryName.Equals("MySQL") ? 3 : 10;
			for (var index = 1; index <= max; index++)
				columns.Add("MediumText" + index.ToString(), new Tuple<Type, int>(typeof(String), 4000));

			max = 5;
			for (var index = 1; index <= max; index++)
				columns.Add("LargeText" + index.ToString(), new Tuple<Type, int>(typeof(String), 0));

			max = dbProviderFactoryName.Equals("MySQL") ? 20 : 40;
			for (var index = 1; index <= max; index++)
				columns.Add("Number" + index.ToString(), new Tuple<Type, int>(typeof(Int32), 0));

			max = 10;
			for (var index = 1; index <= max; index++)
				columns.Add("Decimal" + index.ToString(), new Tuple<Type, int>(typeof(Decimal), 0));

			for (var index = 1; index <= max; index++)
				columns.Add("DateTime" + index.ToString(), new Tuple<Type, int>(typeof(String), 19));

			var sql = "";
			switch (dbProviderFactoryName)
			{
				case "MicrosoftSQL":
					sql = $"CREATE TABLE [{tableName}] ("
						+ string.Join(", ", columns.Select(info =>
						{
							var type = info.Value.Item1;
							var precision = info.Value.Item2;
							var asFixedLength = type.Equals(typeof(String)) && precision.Equals(32);
							var asCLOB = type.Equals(typeof(String)) && precision.Equals(0);
							return "[" + info.Key + "] "
								+ type.GetDbTypeString(dbProviderFactoryName, precision, asFixedLength, asCLOB)
								+ (info.Key.EndsWith("ID") ? " NOT" : "") + " NULL";
						}))
						+ $", CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([ID] ASC) "
						+ "WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, IGNORE_DUP_KEY=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=ON) ON [PRIMARY]) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];\n"
						+ $"CREATE NONCLUSTERED INDEX [IDX_{tableName}] ON [{tableName}] ([ID] ASC, [SystemID] ASC, [RepositoryID] ASC, [EntityID] ASC)"
						+ " WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY];";
					columns.ForEach((info, index) =>
					{
						var type = info.Value.Item1;
						var precision = info.Value.Item2;
						var isBigText = type.Equals(typeof(String)) && precision.Equals(0);
						if (index > 3 && !isBigText)
							sql += "\n"
								+ $"CREATE NONCLUSTERED INDEX [IDX_{tableName}_{info.Key}] ON [{tableName}] ([{info.Key}] ASC)"
								+ " WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY];";
					});
					break;

				case "MySQL":
				case "PostgreSQL":
					sql = $"CREATE TABLE {tableName} ("
						+ string.Join(", ", columns.Select(info =>
						{
							var type = info.Value.Item1;
							var precision = info.Value.Item2;
							var asFixedLength = type.Equals(typeof(String)) && precision.Equals(32);
							var asCLOB = type.Equals(typeof(String)) && precision.Equals(0);
							return info.Key + " "
								+ type.GetDbTypeString(dbProviderFactoryName, precision, asFixedLength, asCLOB)
								+ (info.Key.EndsWith("ID") ? " NOT" : "") + " NULL";
						}))
						+ ", PRIMARY KEY (ID ASC));\n"
						+ $"CREATE INDEX IDX_{tableName} ON {tableName} (ID ASC, SystemID ASC, RepositoryID ASC, EntityID ASC);";
					columns.ForEach((info, index) =>
					{
						var type = info.Value.Item1;
						var precision = info.Value.Item2;
						var isBigText = type.Equals(typeof(String)) && (precision.Equals(0) || precision.Equals(4000));
						if (index > 3 && !isBigText)
							sql += "\n" + $"CREATE INDEX IDX_{tableName}_{info.Key} ON {tableName} ({info.Key} ASC);";
					});
					break;
			}

			// create table
			if (!sql.Equals(""))
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					await connection.OpenAsync().ConfigureAwait(false);
					try
					{
						var command = connection.CreateCommand();
						command.CommandText = sql;
						await command.ExecuteNonQueryAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException("Error occurred while creating new table of extended properties [" + sql + "]", ex);
					}
				}
		}

		internal static async Task EnsureSchemasAsync(this EntityDefinition definition, DataSource dataSource)
		{
			// check existed
			var isExisted = true;
			var dbProviderFactory = dataSource.GetProviderFactory();
			var sql = "";
			switch (dbProviderFactory.GetName())
			{
				case "MicrosoftSQL":
				case "MySQL":
				case "PostgreSQL":
					sql = $"SELECT COUNT(table_name) FROM information_schema.tables WHERE table_name='{definition.TableName.Replace("'", "''")}'";
					break;
			}

			if (!sql.Equals(""))
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					try
					{
						await connection.OpenAsync().ConfigureAwait(false);
						var command = connection.CreateCommand();
						command.CommandText = sql;
						isExisted = (await command.ExecuteScalarAsync().ConfigureAwait(false)).CastAs<int>() > 0;
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException("Error occurred while checking the table existed [" + sql + "]", ex);
					}
				}

			if (!isExisted)
				using (var context = new RepositoryContext(false))
				{
					context.EntityDefinition = definition;

					await context.CreateTableAsync(dataSource).ConfigureAwait(false);

					await context.CreateTableIndexesAsync(dataSource).ConfigureAwait(false);

					if (definition.Searchable)
						await context.CreateTableFulltextIndexAsync(dataSource).ConfigureAwait(false);

					if (definition.ParentType != null && !string.IsNullOrWhiteSpace(definition.ParentAssociatedProperty)
					&& definition.MultipleParentAssociates && !string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesTable)
					&& !string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesMapColumn) && !string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesLinkColumn))
						await context.CreateMapTableAsync(dataSource).ConfigureAwait(false);

					if (definition.Extendable && definition.RepositoryDefinition != null)
						await context.CreateExtentTableAsync(dataSource).ConfigureAwait(false);
				}
		}
		#endregion

	}

	#region --- DbProviderFactories -----
	/// <summary>
	/// The replacement for System.Data.Common.DbProviderFactories
	/// </summary>
	public class DbProviderFactories
	{
		/// <summary>
		/// An instance of a DbProviderFactory for a specified provider name
		/// </summary>
		/// <param name="invariant"></param>
		/// <returns></returns>
		public static DbProviderFactory GetFactory(string invariant)
		{
			if (string.IsNullOrWhiteSpace(invariant))
				throw new ArgumentException("The invariant name is invalid", nameof(invariant));

			if (!DbProviderFactories._ProviderFactories.TryGetValue(invariant, out DbProviderFactory dbProviderFactory))
				lock (DbProviderFactories._ProviderFactories)
				{
					if (!DbProviderFactories._ProviderFactories.TryGetValue(invariant, out dbProviderFactory))
					{
						var provider = DbProviderFactories.Providers.ContainsKey(invariant)
							? DbProviderFactories.Providers[invariant]
							: null;

						if (provider == null)
							throw new NotImplementedException($"Provider ({invariant}) is not installed");
						else if (provider.Type == null)
							throw new InvalidCastException($"Provider ({invariant}) is invalid");

						var field = provider.Type.GetField("Instance", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
						if (field != null && field.FieldType.IsSubclassOf(typeof(DbProviderFactory)))
						{
							var value = field.GetValue(null);
							if (value != null)
							{
								dbProviderFactory = (DbProviderFactory)value;
								DbProviderFactories._ProviderFactories[invariant] = dbProviderFactory;
							}
						}
					}
				}

			return dbProviderFactory ?? throw new NotImplementedException($"Provider ({invariant}) is not installed");
		}

		internal static Dictionary<string, Provider> _Providers = null;
		internal static Dictionary<string, DbProviderFactory> _ProviderFactories = new Dictionary<string, DbProviderFactory>();

		/// <summary>
		/// Gest the current installed of provider factories
		/// </summary>
		public static Dictionary<string, Provider> Providers
		{
			get
			{
				return DbProviderFactories._Providers ?? (DbProviderFactories._Providers = DbProviderFactories.GetProviders());
			}
		}

		internal static Dictionary<string, Provider> GetProviders()
		{
			var providers = new Dictionary<string, Provider>();
			if (ConfigurationManager.GetSection("dbProviderFactories") is AppConfigurationSectionHandler config)
				if (config.Section.SelectNodes("./add") is XmlNodeList nodes)
					foreach (XmlNode node in nodes)
					{
						var info = node.ToJson();
						var invariant = info["invariant"] != null
							? (info["invariant"] as JValue).Value as string
							: null;
						var name = info["name"] != null
							? (info["name"] as JValue).Value as string
							: null;
						var description = info["description"] != null
							? (info["description"] as JValue).Value as string
							: null;
						var type = info["type"] != null
							? Type.GetType((info["type"] as JValue).Value as string)
							: null;

						if (!string.IsNullOrWhiteSpace(invariant) && type != null)
						{
							providers[invariant] = new Provider()
							{
								Invariant = invariant,
								Type = type,
								Name = name,
								Description = description
							};
						}
					}
			return providers;
		}

		public class Provider
		{
			/// <summary>
			/// The name of DbProvider object.
			/// </summary>
			public string Name { get; set; }

			/// <summary>
			/// The invariant of DbProvider object.
			/// </summary>
			public string Invariant { get; set; }

			/// <summary>
			/// The description of DbProvider object.
			/// </summary>
			public string Description { get; set; }

			/// <summary>
			/// The type of DbProvider object.
			/// </summary>
			public Type Type { get; set; }
		}
	}
	#endregion

}