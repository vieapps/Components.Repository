#region Related components
using System;
using System.Linq;
using System.Xml;
using System.Data;
using System.Data.Common;
using System.Transactions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Reflection;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using net.vieapps.Components.Caching;
using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Collection of methods for working with SQL database (support Microsoft SQL Server, Oracle RDBMS, MySQL, and PostgreSQL)
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
			var providerName = dataSource?.ProviderName;
			if (string.IsNullOrWhiteSpace(providerName))
			{
				var connectionStringSettings = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
					? RepositoryMediator.GetConnectionStringSettings(dataSource)
					: null;
				providerName = connectionStringSettings?.ProviderName ?? "System.Data.SqlClient";
			}
			return DbProvider.GetFactory(providerName);
		}

		static bool IsSQLServer(this DbProviderFactory dbProviderFactory)
			=> (dbProviderFactory?.GetTypeName(true) ?? "").Equals("SqlClientFactory");

		static bool IsOracleRDBMS(this DbProviderFactory dbProviderFactory)
			=> (dbProviderFactory?.GetTypeName(true) ?? "").Equals("OracleClientFactory");

		static bool IsMySQL(this DbProviderFactory dbProviderFactory)
			=> (dbProviderFactory?.GetTypeName(true) ?? "").Equals("MySqlConnectorFactory");

		static bool IsPostgreSQL(this DbProviderFactory dbProviderFactory)
			=> (dbProviderFactory?.GetTypeName(true) ?? "").Equals("NpgsqlFactory");

		static bool IsGotRowNumber(this DbProviderFactory dbProviderFactory)
			=> dbProviderFactory != null && (dbProviderFactory.IsSQLServer() || dbProviderFactory.IsOracleRDBMS());

		static bool IsGotLimitOffset(this DbProviderFactory dbProviderFactory)
			=> dbProviderFactory != null && (dbProviderFactory.IsMySQL() || dbProviderFactory.IsPostgreSQL());

		static string GetOffsetStatement(this DbProviderFactory dbProviderFactory, int pageSize, int pageNumber = 1)
			=> dbProviderFactory != null && dbProviderFactory.IsGotLimitOffset()
				? $" LIMIT {pageSize} OFFSET {(pageNumber - 1) * pageSize}"
				: "";

		static string GetName(this DbProviderFactory dbProviderFactory)
			=> dbProviderFactory == null
				? "Unknown"
				: dbProviderFactory.IsSQLServer()
					? "SQLServer"
					: dbProviderFactory.IsMySQL()
						? "MySQL"
						: dbProviderFactory.IsPostgreSQL()
							? "PostgreSQL"
							: dbProviderFactory.IsOracleRDBMS()
								? "OralceRDBMS"
								: $"Unknown [{dbProviderFactory.GetType()}]";
		#endregion

		#region Connection
		/// <summary>
		/// Creates the connection for working with SQL database
		/// </summary>
		/// <param name="dbProviderFactory">The object that presents information of a database provider factory</param>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <param name="openWhenItsCreated">true to open the connection when its created</param>
		/// <returns></returns>
		public static DbConnection CreateConnection(this DbProviderFactory dbProviderFactory, DataSource dataSource, bool openWhenItsCreated = true)
		{
			var connection = dbProviderFactory.CreateConnection();
			connection.ConnectionString = dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL)
				? dataSource.GetConnectionString()?.Replace(StringComparison.OrdinalIgnoreCase, "{database}", dataSource.DatabaseName).Replace(StringComparison.OrdinalIgnoreCase, "{DatabaseName}", dataSource.DatabaseName)
				: null;
			if (openWhenItsCreated)
			{
				if (string.IsNullOrWhiteSpace(connection.ConnectionString))
					throw new ArgumentException("The connection string is invalid");
				connection.Open();
			}
			return connection;
		}

		/// <summary>
		/// Creates the connection for working with SQL database
		/// </summary>
		/// <param name="dbProviderFactory">The object that presents information of a database provider factory</param>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="openWhenItsCreated">true to open the connection when its created</param>
		/// <returns></returns>
		public static async Task<DbConnection> CreateConnectionAsync(this DbProviderFactory dbProviderFactory, DataSource dataSource, CancellationToken cancellationToken = default, bool openWhenItsCreated = true)
		{
			var connection = dbProviderFactory.CreateConnection(dataSource, false);
			if (openWhenItsCreated)
			{
				if (string.IsNullOrWhiteSpace(connection.ConnectionString))
					throw new ArgumentException("The connection string is invalid");
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
			}
			return connection;
		}

		/// <summary>
		/// Gets the connection of SQL database of a specified data-source
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source of SQL database</param>
		/// <returns></returns>
		public static DbConnection GetConnection(this DataSource dataSource)
			=> dataSource?.GetProviderFactory().CreateConnection(dataSource, false);

		/// <summary>
		/// Creates new transaction for working with SQL database
		/// </summary>
		/// <returns></returns>
		public static TransactionScope CreateTransaction()
			=> new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
		#endregion

		#region Command
		internal static DbCommand CreateCommand(this DbConnection connection, string commandText, List<DbParameter> commandParameters = null)
		{
			var command = connection.CreateCommand();
			command.CommandText = commandText;
			commandParameters?.ForEach(parameter => command.Parameters.Add(parameter));
			return command;
		}

		internal static DbCommand CreateCommand(this DbConnection connection, Tuple<string, List<DbParameter>> info)
			=> info != null && !string.IsNullOrWhiteSpace(info.Item1)
				? connection.CreateCommand(info.Item1, info.Item2)
				: null;

		internal static string GetInfo(this DbCommand command, bool addInfo = true)
		{
			var parameters = new List<DbParameter>();
			if (command.Parameters != null)
				foreach (DbParameter parameter in command.Parameters)
					parameters.Add(parameter);

			var statement = command.CommandText ?? "";
			parameters.ForEach(parameter =>
			{
				var pattern = "{0}";
				var value = parameter.Value?.ToString() ?? "NULL";
				if (parameter.DbType.Equals(DbType.String) || parameter.DbType.Equals(DbType.StringFixedLength)
				|| parameter.DbType.Equals(DbType.AnsiString) || parameter.DbType.Equals(DbType.AnsiStringFixedLength) || parameter.DbType.Equals(DbType.DateTime))
				{
					pattern = "'{0}'";
					value = value.Replace(StringComparison.OrdinalIgnoreCase, "'", "''");
				}
				statement = statement.Replace(StringComparison.OrdinalIgnoreCase, parameter.ParameterName, string.Format(pattern, value));
			});

			return (addInfo ? $"SQL Info: {command.Connection.Database} [{command.Connection.GetType()}]" + "\r\n" : "")
				+ "- Command Text: " + (command.CommandText ?? "") + "\r\n"
				+ (parameters.Count < 1 ? "" : "- Command Parameters: \r\n\t+ " + parameters.Select(parameter => $"{parameter.ParameterName} ({parameter.DbType}) => [{parameter.Value ?? "(null)"}]").ToString("\r\n\t+ ") + "\r\n")
				+ "- Command Statement: " + statement;
		}
		#endregion

		#region DbTypes
		internal static Dictionary<Type, DbType> DbTypes { get; } = new Dictionary<Type, DbType>
		{
			{ typeof(string), DbType.String },
			{ typeof(char), DbType.StringFixedLength },
			{ typeof(char?), DbType.StringFixedLength },
			{ typeof(byte), DbType.Byte },
			{ typeof(byte?), DbType.Byte },
			{ typeof(sbyte), DbType.SByte },
			{ typeof(sbyte?), DbType.SByte },
			{ typeof(short), DbType.Int16 },
			{ typeof(short?), DbType.Int16 },
			{ typeof(ushort), DbType.UInt16 },
			{ typeof(ushort?), DbType.UInt16 },
			{ typeof(int), DbType.Int32 },
			{ typeof(int?), DbType.Int32 },
			{ typeof(uint), DbType.UInt32 },
			{ typeof(uint?), DbType.UInt32 },
			{ typeof(long), DbType.Int64 },
			{ typeof(long?), DbType.Int64 },
			{ typeof(ulong), DbType.UInt64 },
			{ typeof(ulong?), DbType.UInt64 },
			{ typeof(float), DbType.Single },
			{ typeof(float?), DbType.Single },
			{ typeof(double), DbType.Double },
			{ typeof(double?), DbType.Double },
			{ typeof(decimal), DbType.Decimal },
			{ typeof(decimal?), DbType.Decimal },
			{ typeof(bool), DbType.Boolean },
			{ typeof(bool?), DbType.Boolean },
			{ typeof(byte[]), DbType.Binary },
			{ typeof(Guid), DbType.Guid },
			{ typeof(Guid?), DbType.Guid },
			{ typeof(DateTime), DbType.DateTime },
			{ typeof(DateTime?), DbType.DateTime },
			{ typeof(DateTimeOffset), DbType.DateTimeOffset },
			{ typeof(DateTimeOffset?), DbType.DateTimeOffset }
		};

		/// <summary>
		/// Gets the database type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static DbType GetDbType(this Type type)
			=> SqlHelper.DbTypes[type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) ? Nullable.GetUnderlyingType(type) : type];

		/// <summary>
		/// Gets the database type
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static DbType GetDbType(this AttributeInfo attribute)
			=> (attribute.Type.IsStringType() && (attribute.MaxLength.Equals(32) || attribute.Name.EndsWith("ID"))) || attribute.IsStoredAsString()
				? DbType.AnsiStringFixedLength
				: attribute.IsStoredAsJson()
					? DbType.String
					: attribute.Type.IsEnum
						? attribute.IsEnumString()
							? DbType.String
							: DbType.Int32
					: attribute.Type.GetDbType();

		/// <summary>
		/// Gets the database type
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static DbType GetDbType(this ExtendedPropertyDefinition attribute)
			=> attribute.Type.Equals(typeof(DateTime))
				? DbType.AnsiString
				: attribute.Type.GetDbType();

		internal static Dictionary<Type, Dictionary<string, string>> DbTypeStrings { get; } = new Dictionary<Type, Dictionary<string, string>>
		{
			{
				typeof(string),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "SQLServer", "NVARCHAR{0}" },
					{ "Default", "VARCHAR{0}" }
				}
			},
			{
				typeof(char),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Default", "CHAR{0}" }
				}
			},
			{
				typeof(char?),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "SQLServer", "NTEXT" },
					{ "Default", "TEXT" }
				}
			},
			{
				typeof(byte),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "PostgreSQL", "SMALLINT" },
					{ "Default", "TINYINT" }
				}
			},
			{
				typeof(sbyte),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "PostgreSQL", "SMALLINT" },
					{ "MySQL", "TINYINT UNSIGNED" },
					{ "Default", "TINYINT" }
				}
			},
			{
				typeof(short),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Default", "SMALLINT" }
				}
			},
			{
				typeof(ushort),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Default", "SMALLINT" }
				}
			},
			{
				typeof(int),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Default", "INT" }
				}
			},
			{
				typeof(uint),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Default", "INT" },
				}
			},
			{
				typeof(long),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Default", "BIGINT" }
				}
			},
			{
				typeof(ulong),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Default", "BIGINT" }
				}
			},
			{
				typeof(float),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "SQLServer", "FLOAT(24)" },
					{ "PostgreSQL", "REAL" },
					{ "Default", "FLOAT" }
				}
			},
			{
				typeof(double),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "SQLServer", "FLOAT(53)" },
					{ "PostgreSQL", "DOUBLE PRECISION" },
					{ "Default", "DOUBLE" }
				}
			},
			{
				typeof(decimal),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "SQLServer", "DECIMAL(19,5)" },
					{ "Default", "NUMERIC(19,5)" }
				}
			},
			{
				typeof(bool),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "SQLServer", "BIT" },
					{ "MySQL", "TINYINT(1)" },
					{ "Default", "BOOLEAN" }
				}
			},
			{
				typeof(DateTime),
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "PostgreSQL", "TIMESTAMP" },
					{ "Default", "DATETIME" }
				}
			}
		};

		internal static string GetDbTypeString(this AttributeInfo attribute, DbProviderFactory dbProviderFactory)
			=> attribute.Type.IsStringType() && attribute.Name.EndsWith("ID") && (attribute.MaxLength.Equals(0) || attribute.MaxLength.Equals(32))
				? typeof(string).GetDbTypeString(dbProviderFactory, 32, true, false)
				: attribute.IsStoredAsString()
					? typeof(string).GetDbTypeString(dbProviderFactory, attribute.IsStoredAsDateOnlyString() ? 10 : 19, true, false)
					: (attribute.IsCLOB != null && attribute.IsCLOB.Value) || attribute.IsStoredAsJson()
						? typeof(string).GetDbTypeString(dbProviderFactory, 0, false, true)
						: attribute.Type.IsEnum
							? attribute.IsEnumString()
								? typeof(string).GetDbTypeString(dbProviderFactory, 50, false, false)
								: typeof(int).GetDbTypeString(dbProviderFactory, 0, false, false)
							: attribute.Type.GetDbTypeString(dbProviderFactory, attribute.MaxLength != null ? attribute.MaxLength.Value : 0);

		internal static string GetDbTypeString(this Type type, DbProviderFactory dbProviderFactory, int precision = 0, bool asFixedLength = false, bool asCLOB = false)
			=> type == null || dbProviderFactory == null
				? ""
				: type.GetDbTypeString(dbProviderFactory.GetName(), precision, asFixedLength, asCLOB);

		internal static string GetDbTypeString(this Type type, string dbProviderFactoryName, int precision = 0, bool asFixedLength = false, bool asCLOB = false)
		{
			type = !type.Equals(typeof(string))
				? type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>))
					? Nullable.GetUnderlyingType(type)
					: type
				: asFixedLength
					? typeof(char)
					: asCLOB
						? typeof(char?)
						: type;

			precision = precision < 1 && type.Equals(typeof(string))
				? 4000
				: precision;

			var dbTypeString = "";
			var dbTypeStrings = !string.IsNullOrWhiteSpace(dbProviderFactoryName) && SqlHelper.DbTypeStrings.ContainsKey(type)
				? SqlHelper.DbTypeStrings[type]
				: null;
			if (dbTypeStrings != null)
			{
				if (!dbTypeStrings.TryGetValue(dbProviderFactoryName, out dbTypeString))
					if (!dbTypeStrings.TryGetValue("Default", out dbTypeString))
						dbTypeString = "";
			}

			return dbTypeString.IndexOf("{0}") > 0 && precision > 0
				? string.Format(dbTypeString, $"({precision})")
				: dbTypeString;
		}
		#endregion

		#region Parameter
		/// <summary>
		/// Creates a parameter
		/// </summary>
		/// <param name="dbProviderFactory"></param>
		/// <param name="name"></param>
		/// <param name="dbType"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, string name, DbType dbType, object value)
		{
			var parameter = dbProviderFactory.CreateParameter();
			parameter.ParameterName = (!name.IsStartsWith("@") ? "@" : "") + name;
			parameter.DbType = dbType;
			parameter.Value = value ?? DBNull.Value;
			return parameter;
		}

		internal static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, KeyValuePair<string, object> info)
			=> dbProviderFactory.CreateParameter(info.Key, info.Key.EndsWith("ID") || info.Key.EndsWith("Id") ? DbType.AnsiStringFixedLength : info.Value.GetType().GetDbType(), info.Value);

		internal static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, AttributeInfo attribute, object value)
			=> dbProviderFactory.CreateParameter(attribute.Name, attribute.GetDbType(), attribute.IsStoredAsJson()
				? value == null
					? ""
					: value.ToJson().ToString(Newtonsoft.Json.Formatting.None)
				: attribute.IsStoredAsString()
					? value == null
						? ""
						: ((DateTime)value).ToDTString(false, attribute.IsStoredAsDateTimeString())
					: value);

		internal static DbParameter CreateParameter(this DbProviderFactory dbProviderFactory, ExtendedPropertyDefinition attribute, object value)
			=> dbProviderFactory.CreateParameter(attribute.Name, attribute.GetDbType(), attribute.Type.Equals(typeof(DateTime))
				? value == null
					? ""
					: ((DateTime)value).ToDTString()
				: value);
		#endregion

		#region Data Adapter
		internal static DbDataAdapter CreateDataAdapter(this DbProviderFactory dbProviderFactory, DbCommand command)
		{
			var dataAdapter = dbProviderFactory.CreateDataAdapter();
			dataAdapter.SelectCommand = command;
			return dataAdapter;
		}
		#endregion

		#region Copy (DataReader/DataRow)
		/// <summary>
		/// Sets the value of an attribute
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object"></param>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <param name="standardAttributes"></param>
		/// <param name="extendedAttributes"></param>
		/// <param name="changedNotifier"></param>
		/// <param name="whenSetAttributeValueGotError"></param>
		public static void SetAttributeValue<T>(this T @object, string name, object value, Dictionary<string, AttributeInfo> standardAttributes, Dictionary<string, ExtendedPropertyDefinition> extendedAttributes, IPropertyChangedNotifier changedNotifier = null, Action<T, string, object, Exception> whenSetAttributeValueGotError = null) where T : class
		{
			try
			{
				if (standardAttributes != null && standardAttributes.ContainsKey(name))
				{
					var attribute = standardAttributes[name];
					if (value != null)
					{
						var strValue = value as string;
						if (attribute.IsDateTimeType() && attribute.IsStoredAsString())
							value = DateTime.Parse(strValue);

						else if (attribute.IsStoredAsJson())
							try
							{
								value = new JsonSerializer().Deserialize(new JTokenReader(JToken.Parse(strValue)), attribute.Type);
							}
							catch
							{
								value = null;
							}

						else if (attribute.IsEnum())
							value = attribute.IsEnumString()
								? strValue.ToEnum(attribute.Type)
								: value.CastAs<int>();

						else if (attribute.IsGenericListOrHashSet())
							value = strValue != null
								? attribute.IsGenericList()
									? strValue.ToList() as object
									: strValue.ToHashSet()
								: value;

						else if (attribute.IsStringType() && string.IsNullOrWhiteSpace(strValue) && !attribute.NotNull)
							value = null;
					}

					@object.SetAttributeValue(attribute, value, true);
					changedNotifier?.NotifyPropertyChanged(name);
				}

				else if (extendedAttributes != null && extendedAttributes.ContainsKey(name))
				{
					var attribute = extendedAttributes[name];
					if (value != null && attribute.Type.IsDateTimeType())
						value = value is DateTime datetime
							? datetime
							: DateTime.Parse(value.ToString());
					(@object as IBusinessEntity).ExtendedProperties[attribute.Name] = value?.CastAs(attribute.Type);
					changedNotifier?.NotifyPropertyChanged(name);
				}
			}
			catch (Exception ex)
			{
				if (whenSetAttributeValueGotError != null)
					whenSetAttributeValueGotError(@object, name, value, ex);
				else
					throw new RepositoryOperationException($"Cannot set the value of an attribute => {ex.Message} [{@object.GetType()}#{@object.GetEntityID()} :: {name} => {value}]", ex);
			}
		}

		/// <summary>
		/// Copies data from data-reader into this object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object"></param>
		/// <param name="dataReader"></param>
		/// <param name="standardAttributes"></param>
		/// <param name="extendedAttributes"></param>
		/// <param name="whenSetAttributeValueGotError"></param>
		/// <returns></returns>
		public static T Copy<T>(this T @object, DbDataReader dataReader, Dictionary<string, AttributeInfo> standardAttributes, Dictionary<string, ExtendedPropertyDefinition> extendedAttributes, Action<T, string, object, Exception> whenSetAttributeValueGotError = null) where T : class
		{
			@object = @object ?? ObjectService.CreateInstance<T>();

			if (@object is IBusinessEntity businessEntity && businessEntity.ExtendedProperties == null && extendedAttributes != null)
				businessEntity.ExtendedProperties = new Dictionary<string, object>();

			var changedNotifier = @object is IPropertyChangedNotifier
				? @object as IPropertyChangedNotifier
				: null;

			for (var index = 0; index < dataReader.FieldCount; index++)
				@object.SetAttributeValue(dataReader.GetName(index), dataReader[index], standardAttributes, extendedAttributes, changedNotifier, whenSetAttributeValueGotError);

			return @object;
		}

		/// <summary>
		/// Copies data from data-row into this object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object"></param>
		/// <param name="dataRow"></param>
		/// <param name="standardAttributes"></param>
		/// <param name="extendedAttributes"></param>
		/// <param name="whenSetAttributeValueGotError"></param>
		/// <returns></returns>
		public static T Copy<T>(this T @object, DataRow dataRow, Dictionary<string, AttributeInfo> standardAttributes, Dictionary<string, ExtendedPropertyDefinition> extendedAttributes, Action<T, string, object, Exception> whenSetAttributeValueGotError = null) where T : class
		{
			@object = @object ?? ObjectService.CreateInstance<T>();

			if (@object is IBusinessEntity businessEntity && businessEntity.ExtendedProperties == null && extendedAttributes != null)
				businessEntity.ExtendedProperties = new Dictionary<string, object>();

			var changedNotifier = @object is IPropertyChangedNotifier
				? @object as IPropertyChangedNotifier
				: null;

			for (var index = 0; index < dataRow.Table.Columns.Count; index++)
				@object.SetAttributeValue(dataRow.Table.Columns[index].ColumnName, dataRow[dataRow.Table.Columns[index].ColumnName], standardAttributes, extendedAttributes, changedNotifier, whenSetAttributeValueGotError);

			return @object;
		}
		#endregion

		#region Mappings
		static List<Tuple<string, List<DbParameter>>> PrepareUpdateMappings(this DbProviderFactory dbProviderFactory, string tableName, string linkColumn, string mapColumn, string linkValue, IEnumerable<string> mapValues)
		{
			var statements = new List<Tuple<string, List<DbParameter>>>
			{
				new Tuple<string, List<DbParameter>>(
					$"DELETE FROM {tableName} WHERE {linkColumn}=@{linkColumn}",
					new List<DbParameter>
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>($"@{linkColumn}", linkValue))
					}
				)
			};
			mapValues.ForEach(mapValue => statements.Add(new Tuple<string, List<DbParameter>>(
				$"INSERT INTO {tableName} ({linkColumn},{mapColumn}) VALUES (@{linkColumn},@{mapColumn})",
				new List<DbParameter>
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>($"@{linkColumn}", linkValue)),
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>($"@{mapColumn}", mapValue))
				}
			)));
			return statements;
		}

		static List<Tuple<string, List<DbParameter>>> PrepareUpdateMappings<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var statements = new List<Tuple<string, List<DbParameter>>>();
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var linkValue = @object is RepositoryBase ? (@object as RepositoryBase).ID : @object.GetEntityID();

			definition.Attributes.Where(attribute => attribute.IsMappings()).ForEach(attribute =>
			{
				var values = @object.GetAttributeValue(attribute);
				var mapValues = values != null
					? values.IsGenericList() ? values as List<string> : (values as HashSet<string>).ToList()
					: new List<string>();
				var mapInfo = attribute.GetMapInfo(definition);
				statements = statements.Concat(dbProviderFactory.PrepareUpdateMappings(mapInfo.Item1, mapInfo.Item2, mapInfo.Item3, linkValue, mapValues)).ToList();
			});

			return statements;
		}

		static string UpdateMappings<T>(this T @object, DbConnection connection, DbProviderFactory dbProviderFactory, bool performCreateNew = true) where T : class
		{
			var info = "";
			var statements = performCreateNew
				? @object.PrepareUpdateMappings(dbProviderFactory)
				: @object.PrepareUpdateMappings(dbProviderFactory).Where(statement => statement.Item1.IsStartsWith("DELETE")).ToList();
			statements.ForEach(statement =>
			{
				var command = connection.CreateCommand(statement);
				command.ExecuteNonQuery();
				if (RepositoryMediator.IsDebugEnabled)
					info += (info != "" ? "\r\n" : "") + command.GetInfo();
			});
			return info;
		}

		static async Task<string> UpdateMappingsAsync<T>(this T @object, DbConnection connection, DbProviderFactory dbProviderFactory, CancellationToken cancellationToken = default, bool performCreateNew = true) where T : class
		{
			var info = "";
			var statements = performCreateNew
				? @object.PrepareUpdateMappings(dbProviderFactory)
				: @object.PrepareUpdateMappings(dbProviderFactory).Where(statement => statement.Item1.IsStartsWith("DELETE")).ToList();
			await statements.ForEachAsync(async (statement, token) =>
			{
				var command = connection.CreateCommand(statement);
				await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
				if (RepositoryMediator.IsDebugEnabled)
					info += (info != "" ? "\r\n" : "") + command.GetInfo();
			}, cancellationToken, true, false).ConfigureAwait(false);
			return info;
		}

		static Tuple<string, List<DbParameter>> PrepareGetMappings(this DbProviderFactory dbProviderFactory, string tableName, string linkColumn, string mapColumn, string linkValue)
			=> new Tuple<string, List<DbParameter>>(
				$"SELECT {mapColumn} FROM {tableName} WHERE {linkColumn}=@{linkColumn}",
				new List<DbParameter>
				{
					dbProviderFactory.CreateParameter(new KeyValuePair<string, object>($"@{linkColumn}", linkValue))
				}
			);

		static List<string> GetMappings(this DbProviderFactory dbProviderFactory, DbConnection connection, string tableName, string linkColumn, string mapColumn, string linkValue)
		{
			var mapValues = new List<string>();
			var command = connection.CreateCommand(dbProviderFactory.PrepareGetMappings(tableName, linkColumn, mapColumn, linkValue));
			using (var dataReader = command.ExecuteReader())
			{
				while (dataReader.Read())
					mapValues.Add(dataReader[0]?.ToString());
			}
			return mapValues;
		}

		static void GetMappings<T>(this T @object, DbConnection connection, DbProviderFactory dbProviderFactory) where T : class
		{
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var linkValue = @object is RepositoryBase ? (@object as RepositoryBase).ID : @object.GetEntityID();
			definition.Attributes.Where(attribute => attribute.IsMappings()).ForEach(attribute =>
			{
				var mapInfo = attribute.GetMapInfo(definition);
				var mapValues = dbProviderFactory.GetMappings(connection, mapInfo.Item1, mapInfo.Item2, mapInfo.Item3, linkValue);
				@object.SetAttributeValue(attribute, attribute.IsGenericHashSet() ? mapValues.ToHashSet() as object : mapValues);
			});
		}

		static async Task<List<string>> GetMappingsAsync(this DbProviderFactory dbProviderFactory, DbConnection connection, string tableName, string linkColumn, string mapColumn, string linkValue, CancellationToken cancellationToken = default)
		{
			var mapValues = new List<string>();
			var command = connection.CreateCommand(dbProviderFactory.PrepareGetMappings(tableName, linkColumn, mapColumn, linkValue));
			using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
			{
				while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
					mapValues.Add(dataReader[0]?.ToString());
			}
			return mapValues;
		}

		static async Task GetMappingsAsync<T>(this T @object, DbConnection connection, DbProviderFactory dbProviderFactory, CancellationToken cancellationToken = default) where T : class
		{
			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var linkValue = @object is RepositoryBase ? (@object as RepositoryBase).ID : @object.GetEntityID();
			await definition.Attributes.Where(attribute => attribute.IsMappings()).ForEachAsync(async (attribute, token) =>
			{
				var mapInfo = attribute.GetMapInfo(definition);
				var mapValues = await dbProviderFactory.GetMappingsAsync(connection, mapInfo.Item1, mapInfo.Item2, mapInfo.Item3, linkValue, token).ConfigureAwait(false);
				@object.SetAttributeValue(attribute, attribute.IsGenericHashSet() ? mapValues.ToHashSet() as object : mapValues);
			}, cancellationToken, true, false).ConfigureAwait(false);
		}
		#endregion

		#region Create
		static Tuple<string, List<DbParameter>> PrepareCreateOrigin<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var values = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach (var attribute in definition.Attributes.Where(attribute => !attribute.IsMappings()).ToList())
			{
				var value = @object.GetAttributeValue(attribute.Name);
				if (value == null && attribute.IsIgnoredIfNull())
					continue;

				columns.Add(string.IsNullOrEmpty(attribute.Column) ? attribute.Name : attribute.Column);
				values.Add($"@{attribute.Name}");
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			var statement = $"INSERT INTO {definition.TableName} ({columns.Join(", ")}) VALUES ({values.Join(", ")})";

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareCreateExtent<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = "ID,SystemID,RepositoryID,RepositoryEntityID".ToList();
			var values = columns.Select(c => $"@{c}").ToList();
			var parameters = new List<DbParameter>
			{
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@SystemID", (@object as IBusinessEntity).SystemID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@RepositoryID", (@object as IBusinessEntity).RepositoryID)),
				dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@RepositoryEntityID", (@object as IBusinessEntity).RepositoryEntityID))
			};

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var attributes = definition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions;
			foreach (var attribute in attributes)
			{
				columns.Add(attribute.Column);
				values.Add($"@{attribute.Name}");

				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name)
					? (@object as IBusinessEntity).ExtendedProperties[attribute.Name]
					: attribute.GetDefaultValue();
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			var statement = $"INSERT INTO {definition.RepositoryDefinition.ExtendedPropertiesTableName} ({columns.Join(", ")}) VALUES ({values.Join(", ")})";

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
				throw new ArgumentNullException(nameof(@object), "The object is null");

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand(@object.PrepareCreateOrigin(dbProviderFactory));
				try
				{
					command.ExecuteNonQuery();
					var info = !RepositoryMediator.IsDebugEnabled ? "" : command.GetInfo();

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(@object.PrepareCreateExtent(dbProviderFactory));
						command.ExecuteNonQuery();
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();
					}

					info += "\r\n" + @object.UpdateMappings(connection, dbProviderFactory);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform CREATE command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform CREATE command [{typeof(T)}#{@object?.GetEntityID()}]", command.GetInfo(), ex);
				}
			}
		}

		/// <summary>
		/// Creates new the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		public static void Create<T>(DataSource dataSource, T @object) where T : class
		{
			using (var context = new RepositoryContext())
			{
				context.Operation = RepositoryOperation.Create;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				context.Create(dataSource, @object);
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
		public static async Task CreateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand(@object.PrepareCreateOrigin(dbProviderFactory));
				try
				{
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					var info = !RepositoryMediator.IsDebugEnabled ? "" : command.GetInfo();

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(@object.PrepareCreateExtent(dbProviderFactory));
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();
					}

					info += "\r\n" + await @object.UpdateMappingsAsync(connection, dbProviderFactory, cancellationToken).ConfigureAwait(false);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform CREATE command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform CREATE command [{typeof(T)}#{@object?.GetEntityID()}]", command.GetInfo(), ex);
				}
			}
		}

		/// <summary>
		/// Creates new the record of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task CreateAsync<T>(DataSource dataSource, T @object, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				context.Operation = RepositoryOperation.Create;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				await context.CreateAsync(dataSource, @object, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Get
		static Tuple<string, List<DbParameter>> PrepareGetOrigin<T>(this T @object, string id, DbProviderFactory dbProviderFactory) where T : class
		{
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var fields = definition.Attributes
				.Where(attribute => !attribute.IsMappings())
				.Where(attribute => !attribute.IsIgnoredIfNull() || (attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) != null))
				.Select(attribute => "Origin." + (string.IsNullOrEmpty(attribute.Column) ? attribute.Name : $"{attribute.Column} AS {attribute.Name}"))
				.ToList();

			var info = Filters<T>.Equals(definition.PrimaryKey, id).GetSqlStatement();
			var statement = $"SELECT {fields.Join(", ")} FROM {definition.TableName} AS Origin WHERE {info.Item1}";
			var parameters = info.Item2.Select(param => dbProviderFactory.CreateParameter(param)).ToList();

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareGetExtent<T>(this T @object, string id, DbProviderFactory dbProviderFactory, List<ExtendedPropertyDefinition> extendedProperties) where T : class
		{
			var fields = extendedProperties.Select(attribute => $"Origin.{attribute.Column} AS {attribute.Name}")
				.Concat(new[] { "Origin.ID" })
				.ToList();

			var info = Filters<T>.Equals("ID", id).GetSqlStatement();
			var statement = $"SELECT {fields.Join(", ")} FROM {RepositoryMediator.GetEntityDefinition<T>().RepositoryDefinition.ExtendedPropertiesTableName} AS Origin WHERE {info.Item1}";
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
				return default;

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var @object = ObjectService.CreateInstance<T>();
				var command = connection.CreateCommand(@object.PrepareGetOrigin(id, dbProviderFactory));
				try
				{
					using (var dataReader = command.ExecuteReader())
					{
						@object = dataReader.Read()
							? @object.Copy(dataReader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null)
							: null;
					}

					var info = !RepositoryMediator.IsDebugEnabled ? "" : command.GetInfo();

					if (@object != null && @object.IsGotExtendedProperties())
					{
						var extendedProperties = context.EntityDefinition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions;
						command = connection.CreateCommand(@object.PrepareGetExtent(id, dbProviderFactory, extendedProperties));
						using (var dataReader = command.ExecuteReader())
						{
							if (dataReader.Read())
								@object = @object.Copy(dataReader, null, extendedProperties.ToDictionary(attribute => attribute.Name));
						}
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();

						@object.GetMappings(connection, dbProviderFactory);
					}

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform SELECT command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});

					return @object;
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform SELECT command [{typeof(T)}#{id}]", command.GetInfo(), ex);
				}
			}
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
		public static async Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default) where T : class
		{
			if (string.IsNullOrEmpty(id))
				return default;

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var @object = ObjectService.CreateInstance<T>();
				var command = connection.CreateCommand(@object.PrepareGetOrigin(id, dbProviderFactory));
				try
				{
					using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
					{
						@object = await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false)
							? @object.Copy(dataReader, context.EntityDefinition.Attributes.ToDictionary(attribute => attribute.Name), null)
							: null;
					}

					var info = !RepositoryMediator.IsDebugEnabled ? "" : command.GetInfo();

					if (@object != null && @object.IsGotExtendedProperties())
					{
						var extendedProperties = context.EntityDefinition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions;
						command = connection.CreateCommand(@object.PrepareGetExtent(id, dbProviderFactory, extendedProperties));
						using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
						{
							if (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
								@object = @object.Copy(dataReader, null, extendedProperties.ToDictionary(attribute => attribute.Name));
						}
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();

						await @object.GetMappingsAsync(connection, dbProviderFactory, cancellationToken).ConfigureAwait(false);
					}

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform SELECT command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});

					return @object;
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform SELECT command [{typeof(T)}#{id}]", command.GetInfo(), ex);
				}
			}
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null) where T : class
		{
			var objects = context.Find(dataSource, filter, sort, 1, 1, businessRepositoryEntityID, false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			var objects = await context.FindAsync(dataSource, filter, sort, 1, 1, businessRepositoryEntityID, false, cancellationToken).ConfigureAwait(false);
			return objects != null && objects.Count > 0
				? objects[0]
				: null;
		}
		#endregion

		#region Get (by definition and identity)
		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <returns></returns>
		public static object Get(DataSource dataSource, EntityDefinition definition, string id)
		{
			var stopwatch = Stopwatch.StartNew();
			var info = "";

			var @object = definition.Type.CreateInstance();

			dataSource = dataSource ?? definition.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var standardProperties = definition.Attributes
					.Where(attribute => !attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) != null)
					.ToDictionary(attribute => attribute.Name);

				var fields = standardProperties.Select(attribute => "Origin." + (string.IsNullOrEmpty(attribute.Value.Column) ? attribute.Value.Name : attribute.Value.Column + " AS " + attribute.Value.Name));
				var command = connection.CreateCommand($"SELECT {fields.Join(", ")} FROM {definition.TableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'");
				using (var dataReader = command.ExecuteReader())
				{
					@object = dataReader.Read()
						? @object.Copy(dataReader, standardProperties, null)
						: null;
				}

				if (RepositoryMediator.IsDebugEnabled)
					info = command.GetInfo();

				if (@object != null && @object.IsGotExtendedProperties())
				{
					var extendedProperties = definition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name);
					fields = extendedProperties.Select(attribute => $"Origin.{attribute.Value.Column} AS {attribute.Value.Name}");
					command = connection.CreateCommand($"SELECT {fields.Join(", ")} FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'");
					using (var dataReader = command.ExecuteReader())
					{
						if (dataReader.Read())
							@object.Copy(dataReader, null, extendedProperties);
					}

					if (RepositoryMediator.IsDebugEnabled)
						info += "\r\n" + command.GetInfo();
				}
			}

			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new[]
				{
					$"SQL: Perform SELECT command successful [{definition.Type}#{id}] @ {dataSource.Name}",
					$"Execution times: {stopwatch.GetElapsedTimes()}",
					info
				});

			return @object;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <returns></returns>
		public static object Get(EntityDefinition definition, string id)
			=> SqlHelper.Get(definition?.GetPrimaryDataSource(), definition, id);

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<object> GetAsync(DataSource dataSource, EntityDefinition definition, string id, CancellationToken cancellationToken = default)
		{
			var stopwatch = Stopwatch.StartNew();
			var info = "";

			var @object = definition.Type.CreateInstance();

			dataSource = dataSource ?? definition.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var standardProperties = definition.Attributes
					.Where(attribute => !attribute.IsIgnoredIfNull() && @object.GetAttributeValue(attribute) != null)
					.ToDictionary(attribute => attribute.Name);

				var fields = standardProperties.Select(attribute => "Origin." + (string.IsNullOrEmpty(attribute.Value.Column) ? attribute.Value.Name : attribute.Value.Column + " AS " + attribute.Value.Name));
				var command = connection.CreateCommand($"SELECT {fields.Join(", ")} FROM {definition.TableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'");
				using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
				{
					@object = await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false)
						? @object.Copy(dataReader, standardProperties, null)
						: null;
				}

				if (RepositoryMediator.IsDebugEnabled)
					info = command.GetInfo();

				if (@object != null && @object.IsGotExtendedProperties())
				{
					var extendedProperties = definition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name);
					fields = extendedProperties.Select(attribute => $"Origin.{attribute.Value.Column} AS {attribute.Value.Name}");
					command = connection.CreateCommand($"SELECT {fields.Join(", ")} FROM {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Origin WHERE Origin.ID='{id.Replace("'", "''")}'");
					using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
					{
						if (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
							@object.Copy(dataReader, null, extendedProperties);
					}

					if (RepositoryMediator.IsDebugEnabled)
						info += "\r\n" + command.GetInfo();
				}
			}

			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new[]
				{
					$"SQL: Perform SELECT command successful [{definition.Type}#{id}] @ {dataSource.Name}",
					$"Execution times: {stopwatch.GetElapsedTimes()}",
					info
				});

			return @object;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<object> GetAsync(EntityDefinition definition, string id, CancellationToken cancellationToken = default)
			=> SqlHelper.GetAsync(definition?.GetPrimaryDataSource(), definition, id, cancellationToken);
		#endregion

		#region Replace
		static Tuple<string, List<DbParameter>> PrepareReplaceOrigin<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			foreach (var attribute in definition.Attributes.Where(attribute => !attribute.IsMappings()).ToList())
			{
				var value = @object.GetAttributeValue(attribute.Name);
				if (attribute.Name.IsEquals(definition.PrimaryKey) || (value == null && attribute.IsIgnoredIfNull()))
					continue;

				columns.Add($"{(string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column)}=@{attribute.Name}");
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			var statement = $"UPDATE {definition.TableName} SET {columns.Join(", ")} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}";
			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>($"@{definition.PrimaryKey}", @object.GetEntityID(definition.PrimaryKey))));

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareReplaceExtent<T>(this T @object, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var parameters = new List<DbParameter>();

			var definition = RepositoryMediator.GetEntityDefinition<T>();
			var attributes = definition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions;
			foreach (var attribute in attributes)
			{
				columns.Add($"{attribute.Column}=@{attribute.Name}");
				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(attribute.Name)
					? (@object as IBusinessEntity).ExtendedProperties[attribute.Name]
					: attribute.GetDefaultValue();
				parameters.Add(dbProviderFactory.CreateParameter(attribute, value));
			}

			var statement = $"UPDATE {definition.RepositoryDefinition.ExtendedPropertiesTableName} SET {columns.Join(", ")} WHERE ID=@ID";
			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)));

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
				throw new ArgumentNullException(nameof(@object), "Cannot update new because the object is null");

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand(@object.PrepareReplaceOrigin(dbProviderFactory));
				try
				{
					command.ExecuteNonQuery();
					var info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(@object.PrepareReplaceExtent(dbProviderFactory));
						command.ExecuteNonQuery();
					}
					if (RepositoryMediator.IsDebugEnabled)
						info += "\r\n" + command.GetInfo();

					info += "\r\n" + @object.UpdateMappings(connection, dbProviderFactory);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform REPLACE command successful [{@object?.GetType()}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform REPLACE command [{typeof(T)}#{@object?.GetEntityID()}]", command.GetInfo(), ex);
				}
				command?.Dispose();
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
		public static async Task ReplaceAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "Cannot update new because the object is null");

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand(@object.PrepareReplaceOrigin(dbProviderFactory));
				try
				{
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					var info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(@object.PrepareReplaceExtent(dbProviderFactory));
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					}
					if (RepositoryMediator.IsDebugEnabled)
						info += "\r\n" + command.GetInfo();

					info += "\r\n" + await @object.UpdateMappingsAsync(connection, dbProviderFactory, cancellationToken).ConfigureAwait(false);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform REPLACE command successful [{@object?.GetType()}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform REPLACE command [{typeof(T)}#{@object?.GetEntityID()}]", command.GetInfo(), ex);
				}
				command?.Dispose();
			}
		}
		#endregion

		#region Update
		static Tuple<List<string>, List<DbParameter>> PrepareUpdateOrigin<T>(this IDictionary<string, object> values, Dictionary<string, AttributeInfo> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var parameters = new List<DbParameter>();
			attributes.ForEach(kvp =>
			{
				var name = kvp.Key;
				var column = string.IsNullOrEmpty(kvp.Value.Column) ? name : kvp.Value.Column;
				if (values.TryGetValue(name, out var value))
					if (value != null || (value == null && !kvp.Value.IsIgnoredIfNull()))
					{
						columns.Add($"{column}=@{name}");
						parameters.Add(dbProviderFactory.CreateParameter(kvp.Value, value));
					}
			});
			return new Tuple<List<string>, List<DbParameter>>(columns, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareUpdateOrigin<T>(this T @object, List<string> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var included = attributes.Select(name => name.StartsWith("ExtendedProperties.") ? name.Replace("ExtendedProperties.", "") : name).ToHashSet();
			var standardProperties = definition.Attributes.Where(attribute => !attribute.IsMappings() && included.Contains(attribute.Name)).ToDictionary(attribute => attribute.Name);

			var values = new Dictionary<string, object>();
			standardProperties.ForEach(kvp => values[kvp.Key] = @object.GetAttributeValue(kvp.Value));
			var data = values.PrepareUpdateOrigin<T>(standardProperties, dbProviderFactory);
			var columns = data.Item1;
			var parameters = data.Item2;

			/*
			var columns = new List<string>();
			var parameters = new List<DbParameter>();

			var standardProperties = definition.Attributes.Where(attribute => !attribute.IsMappings()).ToDictionary(attribute => attribute.Name.ToLower());
			foreach (var attribute in attributes.Select(name => name.StartsWith("ExtendedProperties.") ? name.Replace("ExtendedProperties.", "") : name).ToList())
			{
				if (!standardProperties.ContainsKey(attribute.ToLower()))
					continue;

				var value = @object.GetAttributeValue(standardProperties[attribute.ToLower()].Name);
				if (value == null && standardProperties[attribute.ToLower()].IsIgnoredIfNull())
					continue;

				columns.Add((string.IsNullOrEmpty(standardProperties[attribute.ToLower()].Column) ? standardProperties[attribute.ToLower()].Name : standardProperties[attribute.ToLower()].Column) + "=@" + standardProperties[attribute.ToLower()].Name);
				parameters.Add(dbProviderFactory.CreateParameter(standardProperties[attribute.ToLower()], value));
			}
			*/

			var statement = columns.Count > 0
				? $"UPDATE {definition.TableName} SET {columns.Join(", ")} WHERE {definition.PrimaryKey}=@{definition.PrimaryKey}"
				: null;
			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@" + definition.PrimaryKey, @object.GetEntityID(definition.PrimaryKey))));

			return new Tuple<string, List<DbParameter>>(statement, parameters);
		}

		static Tuple<List<string>, List<DbParameter>> PrepareUpdateExtent<T>(this IDictionary<string, object> values, Dictionary<string, ExtendedPropertyDefinition> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var columns = new List<string>();
			var parameters = new List<DbParameter>();
			attributes.ForEach(kvp =>
			{
				if (values.TryGetValue(kvp.Key, out var value))
				{
					columns.Add($"{kvp.Value.Column}=@{kvp.Key}");
					parameters.Add(dbProviderFactory.CreateParameter(kvp.Value, value));
				}
			});
			return new Tuple<List<string>, List<DbParameter>>(columns, parameters);
		}

		static Tuple<string, List<DbParameter>> PrepareUpdateExtent<T>(this T @object, List<string> attributes, DbProviderFactory dbProviderFactory) where T : class
		{
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var extendedProperties = definition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name);
			var values = new Dictionary<string, object>();
			extendedProperties.ForEach(kvp =>
			{
				values[kvp.Key] = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(kvp.Key)
					? (@object as IBusinessEntity).ExtendedProperties[kvp.Key]
					: extendedProperties[kvp.Key].GetDefaultValue();
			});
			var data = values.PrepareUpdateExtent<T>(extendedProperties, dbProviderFactory);
			var columns = data.Item1;
			var parameters = data.Item2;

			/*
			var columns = new List<string>();
			var parameters = new List<DbParameter>();

			var extendedProperties = definition.BusinessRepositoryEntities[(@object as IBusinessEntity).RepositoryEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name.ToLower());
			foreach (var attribute in attributes.Select(name => name.StartsWith("ExtendedProperties.") ? name.Replace("ExtendedProperties.", "") : name).ToList())
			{
				if (!extendedProperties.ContainsKey(attribute.ToLower()))
					continue;

				columns.Add(extendedProperties[attribute.ToLower()].Column + "=@" + extendedProperties[attribute.ToLower()].Name);
				var value = (@object as IBusinessEntity).ExtendedProperties != null && (@object as IBusinessEntity).ExtendedProperties.ContainsKey(extendedProperties[attribute.ToLower()].Name)
					? (@object as IBusinessEntity).ExtendedProperties[extendedProperties[attribute.ToLower()].Name]
					: extendedProperties[attribute.ToLower()].GetDefaultValue();
				parameters.Add(dbProviderFactory.CreateParameter(extendedProperties[attribute.ToLower()], value));
			}
			*/

			var statement = columns.Count > 0
				? $"UPDATE {definition.RepositoryDefinition.ExtendedPropertiesTableName} SET {columns.Join(", ")} WHERE ID=@ID"
				: null;
			parameters.Add(dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("@ID", (@object as IBusinessEntity).ID)));

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
				throw new ArgumentNullException(nameof(@object), "Cannot update new because the object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No valid update", nameof(attributes));

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand(@object.PrepareUpdateOrigin(attributes, dbProviderFactory));
				try
				{
					if (command != null)
						command.ExecuteNonQuery();
					var info = command == null || !RepositoryMediator.IsDebugEnabled ? "" : command.GetInfo();

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(@object.PrepareUpdateExtent(attributes, dbProviderFactory));
						if (command != null)
							command.ExecuteNonQuery();
					}
					info += command == null || !RepositoryMediator.IsDebugEnabled ? "" : "\r\n" + command.GetInfo();

					info += "\r\n" + @object.UpdateMappings(connection, dbProviderFactory);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform UPDATE command successful [{@object?.GetType()}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform UPDATE command [{typeof(T)}#{@object?.GetEntityID()}]", command == null ? "" : command.GetInfo(), ex);
				}
				command?.Dispose();
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
		public static async Task UpdateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, CancellationToken cancellationToken = default) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "Cannot update new because the object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No valid update", nameof(attributes));

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand(@object.PrepareUpdateOrigin(attributes, dbProviderFactory));
				try
				{
					if (command != null)
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					var info = command == null || !RepositoryMediator.IsDebugEnabled ? "" : command.GetInfo();

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(@object.PrepareUpdateExtent(attributes, dbProviderFactory));
						if (command != null)
							await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					}
					info += command == null || !RepositoryMediator.IsDebugEnabled ? "" : "\r\n" + command.GetInfo();

					info += "\r\n" + await @object.UpdateMappingsAsync(connection, dbProviderFactory, cancellationToken).ConfigureAwait(false);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform UPDATE command successful [{@object?.GetType()}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform UPDATE command [{typeof(T)}#{@object?.GetEntityID()}]", command == null ? "" : command.GetInfo(), ex);
				}
				command?.Dispose();
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
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand(
					$"DELETE FROM {context.EntityDefinition.TableName} WHERE {context.EntityDefinition.PrimaryKey}=@{context.EntityDefinition.PrimaryKey}",
					new List<DbParameter>
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>(context.EntityDefinition.PrimaryKey, @object.GetEntityID(context.EntityDefinition.PrimaryKey)))
					}
				);
				try
				{
					command.ExecuteNonQuery();
					var info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(
							$"DELETE FROM {context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID",
							new List<DbParameter>
							{
								dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("ID", (@object as IBusinessEntity).ID))
							}
						);
						command.ExecuteNonQuery();
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();
					}

					info += "\r\n" + @object.UpdateMappings(connection, dbProviderFactory, false);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform DELETE command successful [{@object?.GetType()}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform DELETE command [{typeof(T)}#{@object?.GetEntityID()}]", command.GetInfo(), ex);
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
		public static async Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand(
					$"DELETE FROM {context.EntityDefinition.TableName} WHERE {context.EntityDefinition.PrimaryKey}=@{context.EntityDefinition.PrimaryKey}",
					new List<DbParameter>
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>(context.EntityDefinition.PrimaryKey, @object.GetEntityID(context.EntityDefinition.PrimaryKey)))
					}
				);
				try
				{
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					var info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";

					if (@object.IsGotExtendedProperties())
					{
						command = connection.CreateCommand(
							$"DELETE FROM {context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID",
							new List<DbParameter>
							{
								dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("ID", (@object as IBusinessEntity).ID))
							}
						);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();
					}

					info += "\r\n" + await @object.UpdateMappingsAsync(connection, dbProviderFactory, cancellationToken, false).ConfigureAwait(false);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform DELETE command successful [{@object?.GetType()}#{@object?.GetEntityID()}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform DELETE command [{typeof(T)}#{@object?.GetEntityID()}]", command.GetInfo(), ex);
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

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand(
					$"DELETE FROM {context.EntityDefinition.TableName} WHERE {context.EntityDefinition.PrimaryKey}=@{context.EntityDefinition.PrimaryKey}",
					new List<DbParameter>()
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>(context.EntityDefinition.PrimaryKey, id))
					}
				);
				try
				{
					command.ExecuteNonQuery();
					var info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";

					if (context.EntityDefinition.Extendable && context.EntityDefinition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName))
					{
						command = connection.CreateCommand(
							$"DELETE FROM {context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID",
							new List<DbParameter>
							{
								dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("ID", id))
							}
						);
						command.ExecuteNonQuery();
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();
					}

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform DELETE command successful [{typeof(T)}#{id}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform DELETE command [{typeof(T)}#{id}]", command.GetInfo(), ex);
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
		public static async Task DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default) where T : class
		{
			if (string.IsNullOrWhiteSpace(id))
				return;

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand(
					$"DELETE FROM {context.EntityDefinition.TableName} WHERE {context.EntityDefinition.PrimaryKey}=@{context.EntityDefinition.PrimaryKey}",
					new List<DbParameter>
					{
						dbProviderFactory.CreateParameter(new KeyValuePair<string, object>(context.EntityDefinition.PrimaryKey, id))
					}
				);
				try
				{
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					var info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";

					if (context.EntityDefinition.Extendable && context.EntityDefinition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName))
					{
						command = connection.CreateCommand(
							$"DELETE FROM {context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID=@ID",
							new List<DbParameter>()
							{
								dbProviderFactory.CreateParameter(new KeyValuePair<string, object>("ID", id))
							}
						);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						if (RepositoryMediator.IsDebugEnabled)
							info += "\r\n" + command.GetInfo();
					}

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform DELETE command successful [{typeof(T)}#{id}] @ {dataSource.Name}",
							$"Execution times: {stopwatch.GetElapsedTimes()}",
							info
						});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform DELETE command [{typeof(T)}#{id}]", command.GetInfo(), ex);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
		{
			if (filter == null)
				return;

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var info = "";
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand();
				try
				{
					var statement = filter.GetSqlStatement();

					if (context.EntityDefinition.Extendable && context.EntityDefinition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName))
					{
						command = connection.CreateCommand(
							$"DELETE FROM {context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID IN "
								+ $"(SELECT {context.EntityDefinition.PrimaryKey} FROM {context.EntityDefinition.TableName} WHERE {statement.Item1.Replace("Origin.", "")})",
							statement.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
						);
						command.ExecuteNonQuery();
						info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";
					}

					command = connection.CreateCommand(
						$"DELETE FROM {context.EntityDefinition.TableName} WHERE {statement.Item1.Replace("Origin.", "")}",
						statement.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
					);
					command.ExecuteNonQuery();
					info += "\r\n" + command.GetInfo();

					stopwatch.Stop();
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform DELETE MANY command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform DELETE command [{typeof(T)}]", command.GetInfo(), ex);
				}
			}
		}

		/// <summary>
		/// Delete the record of the objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static async Task DeleteManyAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			if (filter == null)
				return;

			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var info = "";
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand();
				try
				{
					var statement = filter.GetSqlStatement();

					if (context.EntityDefinition.Extendable && context.EntityDefinition.RepositoryDefinition != null && !string.IsNullOrWhiteSpace(context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName))
					{
						command = connection.CreateCommand(
							$"DELETE FROM {context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName} WHERE ID IN "
								+ $"SELECT {context.EntityDefinition.PrimaryKey} FROM {context.EntityDefinition.TableName} WHERE {statement.Item1.Replace("Origin.", "")}",
							statement.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
						);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";
					}

					command = connection.CreateCommand(
						$"DELETE FROM {context.EntityDefinition.TableName} WHERE {statement.Item1.Replace("Origin.", "")}",
						statement.Item2.Select(kvp => dbProviderFactory.CreateParameter(kvp)).ToList()
					);
					await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
					info += "\r\n" + command.GetInfo();

					stopwatch.Stop();
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform DELETE MANY command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform DELETE command [{typeof(T)}]", command.GetInfo(), ex);
				}
			}
		}
		#endregion

		#region Select
		static Tuple<string, List<DbParameter>> PrepareSelect<T>(this DbProviderFactory dbProviderFactory, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, definition, true);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var parentIDs = definition != null && autoAssociateWithMultipleParents && filter != null
				? filter.GetAssociatedParentIDs(definition)
				: null;
			var gotAssociateWithMultipleParents = parentIDs != null && parentIDs.Count > 0;

			var statementsInfo = RepositoryExtensions.PrepareSqlStatements(filter, sort, businessRepositoryEntityID, autoAssociateWithMultipleParents, definition, parentIDs, propertiesInfo);

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
			var mapInfo = definition.GetMultiParentMappingsAttribute()?.GetMapInfo(definition);
			var tables = $" FROM {definition.TableName} AS Origin"
				+ (extendedProperties != null ? $" LEFT JOIN {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Extent ON Origin.{definition.PrimaryKey}=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? $" LEFT JOIN {mapInfo.Item1} AS Link ON Origin.{definition.PrimaryKey}=Link.{mapInfo.Item2}" : "");

			// filtering expressions (WHERE)
			var where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? $" WHERE {statementsInfo.Item1.Item1}"
				: "";

			// ordering expressions (ORDER BY)
			var orderby = statementsInfo.Item2;

			// statements
			var select = $"SELECT {(gotAssociateWithMultipleParents ? "DISTINCT " : "")}" + columns.Join(", ") + tables + where;
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
				statement = $"SELECT {fields.Join(", ")},"
					+ $" ROW_NUMBER() OVER(ORDER BY {(!string.IsNullOrWhiteSpace(orderby) ? orderby : definition.PrimaryKey + " ASC")}) AS __RowNumber"
					+ $" FROM ({select}) AS __Records";

				statement = $"SELECT {fields.Join(", ")} FROM ({statement}) AS __Results"
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

		internal static DataTable CreateDataTable(this DbDataReader dataReader, string name = "Table", bool doLoad = false)
		{
			var dataTable = new DataTable(name);

			if (doLoad)
				dataTable.Load(dataReader);

			else
				foreach (DataRow info in dataReader.GetSchemaTable().Rows)
				{
					var dataColumn = new DataColumn
					{
						ColumnName = info["ColumnName"].ToString(),
						Unique = Convert.ToBoolean(info["IsUnique"]),
						AllowDBNull = Convert.ToBoolean(info["AllowDBNull"]),
						ReadOnly = Convert.ToBoolean(info["IsReadOnly"]),
						DataType = (Type)info["DataType"]
					};
					dataTable.Columns.Add(dataColumn);
				}

			return dataTable;
		}

		internal static void Append(this DataTable dataTable, DbDataReader dataReader)
		{
			var data = new object[dataReader.FieldCount];
			for (var index = 0; index < dataReader.FieldCount; index++)
				data[index] = dataReader[index];
			dataTable.LoadDataRow(data, true);
		}

		internal static DataTable ToDataTable<T>(this DbDataReader dataReader) where T : class
		{
			var dataTable = dataReader.CreateDataTable(typeof(T).GetTypeName(true));
			while (dataReader.Read())
				dataTable.Append(dataReader);
			return dataTable;
		}

		internal static async Task<DataTable> ToDataTableAsync<T>(this DbDataReader dataReader, CancellationToken cancellationToken = default) where T : class
		{
			var dataTable = dataReader.CreateDataTable(typeof(T).GetTypeName(true));
			while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
				dataTable.Append(dataReader);
			return dataTable;
		}

		internal static List<DataRow> ToList(this DataRowCollection dataRows)
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
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include all attributes)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static List<DataRow> Select<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var info = "";
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				DataTable dataTable = null;
				var statement = dbProviderFactory.PrepareSelect(attributes, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset() || pageSize < 1)
				{
					var command = connection.CreateCommand(statement);
					try
					{
						using (var dataReader = command.ExecuteReader())
						{
							dataTable = dataReader.ToDataTable<T>();
						}
						info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SELECT command [{typeof(T)}]", command.GetInfo(), ex);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter(connection.CreateCommand(statement));
					try
					{
						var dataSet = new DataSet();
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						dataTable = dataSet.Tables[0];
						if (RepositoryMediator.IsDebugEnabled)
							info = dataAdapter.SelectCommand.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SELECT command [{typeof(T)}]", dataAdapter.SelectCommand.GetInfo(), ex);
					}
				}

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform SELECT command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Total of results: {dataTable.Rows.Count} - Page number: {pageNumber} - Page size: {pageSize} - Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<DataRow>> SelectAsync<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var info = "";
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				DataTable dataTable = null;
				var statement = dbProviderFactory.PrepareSelect(attributes, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset() || pageSize < 1)
				{
					var command = connection.CreateCommand(statement);
					try
					{
						using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
						{
							dataTable = await dataReader.ToDataTableAsync<T>(cancellationToken).ConfigureAwait(false);
						}
						info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SELECT command [{typeof(T)}]", command.GetInfo(), ex);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter(connection.CreateCommand(statement));
					try
					{
						var dataSet = new DataSet();
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						dataTable = dataSet.Tables[0];
						if (RepositoryMediator.IsDebugEnabled)
							info = dataAdapter.SelectCommand.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SELECT command [{typeof(T)}]", dataAdapter.SelectCommand.GetInfo(), ex);
					}
				}

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform SELECT command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Total of results: {dataTable.Rows.Count} - Page number: {pageNumber} - Page size: {pageSize} - Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
			=> SqlHelper.Select(context, dataSource, new[] { context.EntityDefinition.PrimaryKey }, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents)
				.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
				.ToList();

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default) where T : class
			=> (await SqlHelper.SelectAsync(context, dataSource, new[] { context.EntityDefinition.PrimaryKey }, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false))
				.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
				.ToList();
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			var standardProperties = context.EntityDefinition.Attributes.Where(attribute => !attribute.IsMappings()).ToDictionary(attribute => attribute.Name);
			var extendedProperties = !string.IsNullOrWhiteSpace(businessRepositoryEntityID) && context.EntityDefinition.BusinessRepositoryEntities.ContainsKey(businessRepositoryEntityID)
				? context.EntityDefinition.BusinessRepositoryEntities[businessRepositoryEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name)
				: null;

			var results = new List<T>();
			var parentMappingProperty = context.EntityDefinition.Attributes.FirstOrDefault(attr => attr.IsParentMapping())?.Name;
			if (autoAssociateWithMultipleParents && !string.IsNullOrWhiteSpace(parentMappingProperty))
			{
				var allAttributes = standardProperties
					.Select(info => info.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>());

				var distinctAttributes = new List<string>(sort != null ? sort.GetAttributes() : new List<string>()) { context.EntityDefinition.PrimaryKey }
					.Distinct()
					.ToList();

				var objects = context.Select(dataSource, distinctAttributes, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents)
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToDictionary(@object => @object.GetEntityID(context.EntityDefinition.PrimaryKey));

				var otherAttributes = allAttributes.Except(distinctAttributes).ToList();
				if (otherAttributes.Count > 0)
				{
					otherAttributes.Add(context.EntityDefinition.PrimaryKey);
					context.Select(dataSource, otherAttributes, Filters<T>.Or(objects.Select(item => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, item.Key))), null, 0, 1, businessRepositoryEntityID, false)
						.ForEach(data =>
						{
							var id = data[context.EntityDefinition.PrimaryKey].CastAs<string>();
							objects[id] = objects[id].Copy(data, standardProperties, extendedProperties);
						});
				}

				results = objects.Select(item => item.Value).ToList();
			}
			else
				results = context.Select(dataSource, null, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents)
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToList();

			if (results.Count > 0 && context.EntityDefinition.Attributes.Count(attribute => attribute.IsMappings()) > 0)
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					results.ForEach(@object => @object.GetMappings(connection, dbProviderFactory));
				}
			}

			return results;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default) where T : class
		{
			var standardProperties = context.EntityDefinition.Attributes.Where(attribute => !attribute.IsMappings()).ToDictionary(attribute => attribute.Name);
			var extendedProperties = !string.IsNullOrWhiteSpace(businessRepositoryEntityID) && context.EntityDefinition.BusinessRepositoryEntities.ContainsKey(businessRepositoryEntityID)
				? context.EntityDefinition.BusinessRepositoryEntities[businessRepositoryEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => attribute.Name)
				: null;

			var results = new List<T>();
			var parentMappingProperty = context.EntityDefinition.Attributes.FirstOrDefault(attr => attr.IsParentMapping())?.Name;
			if (autoAssociateWithMultipleParents && !string.IsNullOrWhiteSpace(parentMappingProperty))
			{
				var allAttributes = standardProperties
					.Select(info => info.Value.Name)
					.Concat(extendedProperties != null ? extendedProperties.Select(info => info.Value.Name) : new List<string>());

				var distinctAttributes = new List<string>(sort != null ? sort.GetAttributes() : new List<string>()) { context.EntityDefinition.PrimaryKey }
					.Distinct()
					.ToList();

				var objects = (await context.SelectAsync(dataSource, distinctAttributes, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false))
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToDictionary(@object => @object.GetEntityID(context.EntityDefinition.PrimaryKey));

				var otherAttributes = allAttributes.Except(distinctAttributes).ToList();
				if (otherAttributes.Count > 0)
				{
					otherAttributes.Add(context.EntityDefinition.PrimaryKey);
					(await context.SelectAsync(dataSource, otherAttributes, Filters<T>.Or(objects.Select(item => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, item.Key))), null, 0, 1, businessRepositoryEntityID, false, cancellationToken).ConfigureAwait(false))
						.ForEach(data =>
						{
							var id = data[context.EntityDefinition.PrimaryKey].CastAs<string>();
							objects[id] = objects[id].Copy(data, standardProperties, extendedProperties);
						});
				}

				results = objects.Select(item => item.Value).ToList();
			}
			else
				results = (await context.SelectAsync(dataSource, null, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false))
					.Select(data => ObjectService.CreateInstance<T>().Copy(data, standardProperties, extendedProperties))
					.ToList();

			if (results.Count > 0 && context.EntityDefinition.Attributes.Count(attribute => attribute.IsMappings()) > 0)
			{
				var dbProviderFactory = dataSource.GetProviderFactory();
				using (var connection = dbProviderFactory.CreateConnection(dataSource))
				{
					await results.ForEachAsync(async (@object, token) =>
					{
						await @object.GetMappingsAsync(connection, dbProviderFactory, token).ConfigureAwait(false);
					}, cancellationToken).ConfigureAwait(false);
				}
			}

			return results;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessRepositoryEntityID = null) where T : class
			=> identities == null || identities.Count < 1
				? new List<T>()
				: context.Find(dataSource, Filters<T>.Or(identities.Select(id => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, id))), sort, 0, 1, businessRepositoryEntityID, false);

		/// <summary>
		/// Finds the records and construct the collection of objects that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
			=> identities == null || identities.Count < 1
				? Task.FromResult(new List<T>())
				: context.FindAsync(dataSource, Filters<T>.Or(identities.Select(id => Filters<T>.Equals(context.EntityDefinition.PrimaryKey, id))), sort, 0, 1, businessRepositoryEntityID, false, cancellationToken);
		#endregion

		#region Count
		static Tuple<string, List<DbParameter>> PrepareCount<T>(this DbProviderFactory dbProviderFactory, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, definition);

			var parentIDs = definition != null && autoAssociateWithMultipleParents && filter != null
				? filter.GetAssociatedParentIDs(definition)
				: null;
			var gotAssociateWithMultipleParents = parentIDs != null && parentIDs.Count > 0;

			var statementsInfo = RepositoryExtensions.PrepareSqlStatements(filter, null, businessRepositoryEntityID, autoAssociateWithMultipleParents, definition, parentIDs, propertiesInfo);

			// tables (FROM)
			var mapInfo = definition.GetMultiParentMappingsAttribute()?.GetMapInfo(definition);
			var tables = $" FROM {definition.TableName} AS Origin"
				+ (propertiesInfo.Item2 != null ? $" LEFT JOIN {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Extent ON Origin.{definition.PrimaryKey}=Extent.ID" : "")
				+ (gotAssociateWithMultipleParents ? $" LEFT JOIN {mapInfo.Item1} AS Link ON Origin.{definition.PrimaryKey}=Link.{mapInfo.Item2}" : "");

			// couting expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// statement
			var statement = $"SELECT COUNT({(gotAssociateWithMultipleParents ? "DISTINCT " : "")}{definition.PrimaryKey}) AS TotalRecords{tables}{where}";

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand(dbProviderFactory.PrepareCount(filter, businessRepositoryEntityID, autoAssociateWithMultipleParents));
				try
				{
					var total = command.ExecuteScalar().CastAs<long>();

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform COUNT command successful [{typeof(T)}] @ {dataSource.Name}",
							$"Total: {total} - Execution times: {stopwatch.GetElapsedTimes()}",
							command.GetInfo()
						});

					return total;
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform COUNT command [{typeof(T)}]", command.GetInfo(), ex);
				}
			}
		}

		/// <summary>
		/// Counts the number of all matched records
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, CancellationToken cancellationToken = default) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand(dbProviderFactory.PrepareCount(filter, businessRepositoryEntityID, autoAssociateWithMultipleParents));
				try
				{
					var total = (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>();

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform COUNT command successful [{typeof(T)}] @ {dataSource.Name}",
							$"Total: {total} - Execution times: {stopwatch.GetElapsedTimes()}",
							command.GetInfo()
						});

					return total;
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform COUNT command [{typeof(T)}]", command.GetInfo(), ex);
				}
			}
		}
		#endregion

		#region Search
		static string GetSearchTerms(this DbProviderFactory dbProviderFactory, SearchQuery searchQuery)
		{
			var searchTerms = "";

			// SQL Server
			if (dbProviderFactory.IsSQLServer())
			{
				// prepare AND terms
				var andSearchTerms = "";
				searchQuery.AndWords.ForEach(word => andSearchTerms += "\"*" + word + "*\"" + " AND ");
				searchQuery.AndPhrases.ForEach(phrase => andSearchTerms += "\"" + phrase + "\" AND ");
				if (!andSearchTerms.Equals(""))
					andSearchTerms = andSearchTerms.Left(andSearchTerms.Length - 5);

				// prepare NOT terms
				var notSearchTerms = "";
				searchQuery.NotWords.ForEach(word => notSearchTerms += " AND NOT " + word);
				searchQuery.NotPhrases.ForEach(phrase => notSearchTerms += " AND NOT \"" + phrase + "\"");
				if (!notSearchTerms.Equals(""))
					notSearchTerms = notSearchTerms.Trim();

				// prepare OR terms
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
				// prepare AND terms
				var andSearchTerms = "";
				searchQuery.AndWords.ForEach(word => andSearchTerms += (andSearchTerms.Equals("") ? "" : " ") + "+" + word);
				searchQuery.AndPhrases.ForEach(phrase => andSearchTerms += (andSearchTerms.Equals("") ? "" : " ") + "+\"" + phrase + "\"");

				// prepare NOT terms
				var notSearchTerms = "";
				searchQuery.NotWords.ForEach(word => notSearchTerms += (notSearchTerms.Equals("") ? "" : " ") + "-" + word);
				searchQuery.NotPhrases.ForEach(phrase => notSearchTerms += (notSearchTerms.Equals("") ? "" : " ") + "-\"" + phrase + "\"");

				// prepare OR terms
				var orSearchTerms = "";
				searchQuery.OrWords.ForEach(word => orSearchTerms += (orSearchTerms.Equals("") ? "" : " ") + word);
				searchQuery.OrPhrases.ForEach(phrase => orSearchTerms += (orSearchTerms.Equals("") ? "" : " ") + "\"" + phrase + "\"");

				// build search terms
				searchTerms = ((andSearchTerms + " " + notSearchTerms).Trim() + " " + orSearchTerms).Trim();
			}

			// PostgreSQL
			else if (dbProviderFactory.IsPostgreSQL())
			{
				// prepare AND terms
				var andSearchTerms = "";
				searchQuery.AndWords.ForEach(word => andSearchTerms += (andSearchTerms.Equals("") ? "" : " & ") + word);
				searchQuery.AndPhrases.ForEach(phrase => andSearchTerms += (andSearchTerms.Equals("") ? "" : " & ") + "(" + phrase.Replace(" ", "<->") + ")");

				// prepare NOT terms
				var notSearchTerms = "";
				searchQuery.NotWords.ForEach(word => notSearchTerms += (notSearchTerms.Equals("") ? "" : " !") + word);
				searchQuery.NotPhrases.ForEach(phrase => notSearchTerms += (notSearchTerms.Equals("") ? "" : " !") + "(" + phrase.Replace(" ", "<->") + ")");

				// prepare OR terms
				var orSearchTerms = "";
				searchQuery.OrWords.ForEach(word => orSearchTerms += (orSearchTerms.Equals("") ? "" : " | ") + word);
				searchQuery.OrPhrases.ForEach(phrase => orSearchTerms += (orSearchTerms.Equals("") ? "" : " | ") + "(" + phrase.Replace(" ", "<->") + ")");

				// build search terms
				if (!andSearchTerms.Equals("") && !notSearchTerms.Equals("") && !orSearchTerms.Equals(""))
					searchTerms = andSearchTerms + " & (" + notSearchTerms + ") & (" + orSearchTerms + ")";

				else if (andSearchTerms.Equals("") && orSearchTerms.Equals("") && !notSearchTerms.Equals(""))
					searchTerms = "";

				else if (andSearchTerms.Equals(""))
				{
					searchTerms = orSearchTerms;
					if (!notSearchTerms.Equals(""))
					{
						if (!searchTerms.Equals(""))
							searchTerms = "(" + searchTerms + ") ";
						searchTerms += " & (" + notSearchTerms + ")";
					}
				}

				else
				{
					searchTerms = andSearchTerms;
					if (!notSearchTerms.Equals(""))
					{
						if (!searchTerms.Equals(""))
							searchTerms += " & ";
						searchTerms += notSearchTerms;
					}
					if (!orSearchTerms.Equals(""))
					{
						if (!searchTerms.Equals(""))
							searchTerms += " & (" + orSearchTerms + ")";
						else
							searchTerms += orSearchTerms;
					}
				}
			}

			return searchTerms;
		}

		static Tuple<string, List<DbParameter>> PrepareSearch<T>(this DbProviderFactory dbProviderFactory, string query, IFilterBy<T> filter, SortBy<T> sort, IEnumerable<string> attributes, int pageSize, int pageNumber, string businessRepositoryEntityID = null, string searchInColumns = "*") where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, definition, true);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var statementsInfo = RepositoryExtensions.PrepareSqlStatements(filter, null, businessRepositoryEntityID, false, definition, null, propertiesInfo);

			// fields/columns (SELECT)
			var fields = (attributes != null && attributes.Any()
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
			var orderby = "SearchScore DESC";
			if (sort != null)
				orderby = sort.GetSqlStatement(standardProperties, extendedProperties) + ", " + orderby;

			// searching terms
			var searchTerms = dbProviderFactory.GetSearchTerms(new SearchQuery(query));

			// SQL Server
			if (dbProviderFactory.IsSQLServer())
			{
				fields.Add("SearchScore");
				columns.Add("Search.[RANK] AS SearchScore");
				tables += $" INNER JOIN CONTAINSTABLE ({definition.TableName}, {searchInColumns}, {searchTerms}) AS Search ON Origin.{definition.PrimaryKey}=Search.[KEY]";
			}

			// MySQL
			else if (dbProviderFactory.IsMySQL())
			{
				searchInColumns = !searchInColumns.Equals("*")
					? searchInColumns
					: standardProperties.Where(attribute => attribute.Value.IsSearchable()).Select(attribute => "Origin." + attribute.Value.Name).Join(", ");
				fields.Add("SearchScore");
				columns.Add($"(MATCH({searchInColumns}) AGAINST ({searchTerms} IN BOOLEAN MODE) AS SearchScore");
				where += (!where.Equals("") ? " AND " : " WHERE ") + "SearchScore > 0";
			}

			// PostgreSQL
			else if (dbProviderFactory.IsPostgreSQL())
			{
				searchInColumns = !searchInColumns.Equals("*")
					? searchInColumns
					: standardProperties.Where(attribute => attribute.Value.IsSearchable()).Select(attribute => "Origin." + attribute.Value.Name).Join(" || ");
				fields.Add("SearchScore");
				columns.Add($"ts_rank_cd(to_tsvector('english', {searchInColumns} || ' '), to_tsquery('{searchTerms.Replace("'", "''")}')) AS SearchScore");
				where += (!where.Equals("") ? " AND " : " WHERE ") + $"to_tsvector('english', {searchInColumns} || ' ') @@ to_tsquery('{searchTerms.Replace("'", "''")}')";
			}

			// statement
			var select = "SELECT " + columns.Join(", ") + tables + where;
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
							orderby += (orderby.Equals("") ? "" : ", ") + extendedProperties[info[0]].Column + (info.Length > 1 ? " " + info[1] : "");
						else
							orderby += (orderby.Equals("") ? "" : ", ") + order;
					});
				}

				// set pagination statement
				statement = $"SELECT {fields.Join(", ")}, ROW_NUMBER() OVER(ORDER BY {(!string.IsNullOrWhiteSpace(orderby) ? orderby : definition.PrimaryKey + " ASC")}) AS __RowNumber"
					+ $" FROM ({select}) AS __Records";

				statement = $"SELECT {fields.Join(", ")} FROM ({statement}) AS __Results"
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
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var info = "";
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var statement = dbProviderFactory.PrepareSearch(query, filter, sort, null, pageSize, pageNumber, businessRepositoryEntityID);
				DataTable dataTable = null;

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset() || pageSize < 1)
				{
					var command = connection.CreateCommand(statement);
					try
					{
						using (var dataReader = command.ExecuteReader())
						{
							dataTable = dataReader.ToDataTable<T>();
						}
						info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", command.GetInfo(), ex);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter(connection.CreateCommand(statement));
					try
					{
						var dataSet = new DataSet();
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						dataTable = dataSet.Tables[0];
						if (RepositoryMediator.IsDebugEnabled)
							info = dataAdapter.SelectCommand.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", dataAdapter.SelectCommand.GetInfo(), ex);
					}
				}

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform SEARCH command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Query: {query}",
						$"Total of results: {dataTable.Rows.Count} - Page number: {pageNumber} - Page size: {pageSize} - Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});

				var results = dataTable.Rows.ToList().Select(dataRow => ObjectService.CreateInstance<T>().Copy(dataRow, standardProperties, extendedProperties)).ToList();
				if (results.Count > 0 && context.EntityDefinition.Attributes.Any(attribute => attribute.IsMappings()))
					results.ForEach(@object => @object.GetMappings(connection, dbProviderFactory));

				return results;
			}
		}

		/// <summary>
		/// Searchs the records and construct the collection of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var info = "";
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var statement = dbProviderFactory.PrepareSearch(query, filter, sort, null, pageSize, pageNumber, businessRepositoryEntityID);
				DataTable dataTable = null;

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset() || pageSize < 1)
				{
					var command = connection.CreateCommand(statement);
					try
					{
						using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
						{
							dataTable = await dataReader.ToDataTableAsync<T>(cancellationToken).ConfigureAwait(false);
						}
						info = RepositoryMediator.IsDebugEnabled ? command.GetInfo() : "";
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", command.GetInfo(), ex);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter(connection.CreateCommand(statement));
					try
					{
						var dataSet = new DataSet();
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						dataTable = dataSet.Tables[0];
						if (RepositoryMediator.IsDebugEnabled)
							info = dataAdapter.SelectCommand.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", dataAdapter.SelectCommand.GetInfo(), ex);
					}
				}

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform SEARCH command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Query: {query}",
						$"Total of results: {dataTable.Rows.Count} - Page number: {pageNumber} - Page size: {pageSize} - Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});

				var results = dataTable.Rows
					.ToList()
					.Select(dataRow => ObjectService.CreateInstance<T>().Copy(dataRow, standardProperties, extendedProperties))
					.ToList();

				if (results.Count > 0 && context.EntityDefinition.Attributes.Count(attribute => attribute.IsMappings()) > 0)
					await results.ForEachAsync(async (@object, token) =>
					{
						await @object.GetMappingsAsync(connection, dbProviderFactory, token).ConfigureAwait(false);
					}, cancellationToken).ConfigureAwait(false);

				return results;
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
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			var info = "";
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var identities = new List<string>();
				var statement = dbProviderFactory.PrepareSearch(query, filter, sort, new[] { context.EntityDefinition.PrimaryKey }, pageSize, pageNumber, businessRepositoryEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset() || pageSize < 1)
				{
					var command = connection.CreateCommand(statement);
					try
					{
						using (var dataReader = command.ExecuteReader())
						{
							while (dataReader.Read())
								identities.Add(dataReader[context.EntityDefinition.PrimaryKey].CastAs<string>());
						}
						if (RepositoryMediator.IsDebugEnabled)
							info = command.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", command.GetInfo(), ex);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter(connection.CreateCommand(statement));
					try
					{
						var dataSet = new DataSet();
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						identities = dataSet.Tables[0].Rows.ToList()
							.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
							.ToList();
						if (RepositoryMediator.IsDebugEnabled)
							info = dataAdapter.SelectCommand.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", dataAdapter.SelectCommand.GetInfo(), ex);
					}
				}

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform SEARCH command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Query: {query}",
						$"Total of results: {identities.Count} - Page number: {pageNumber} - Page size: {pageSize} - Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});

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
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SearchIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			var info = "";
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, context.EntityDefinition);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var identities = new List<string>();
				var statement = dbProviderFactory.PrepareSearch(query, filter, sort, new[] { context.EntityDefinition.PrimaryKey }, pageSize, pageNumber, businessRepositoryEntityID);

				// got ROW_NUMBER or LIMIT ... OFFSET
				if (dbProviderFactory.IsGotRowNumber() || dbProviderFactory.IsGotLimitOffset() || pageSize < 1)
				{
					var command = connection.CreateCommand(statement);
					try
					{
						using (var dataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
						{
							while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
								identities.Add(dataReader[context.EntityDefinition.PrimaryKey].CastAs<string>());
						}
						if (RepositoryMediator.IsDebugEnabled)
							info = command.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", command.GetInfo(), ex);
					}
				}

				// generic SQL
				else
				{
					var dataAdapter = dbProviderFactory.CreateDataAdapter(connection.CreateCommand(statement));
					try
					{
						var dataSet = new DataSet();
						dataAdapter.Fill(dataSet, pageNumber > 0 ? (pageNumber - 1) * pageSize : 0, pageSize, typeof(T).GetTypeName(true));
						identities = dataSet.Tables[0].Rows.ToList()
							.Select(data => data[context.EntityDefinition.PrimaryKey].CastAs<string>())
							.ToList();

						if (RepositoryMediator.IsDebugEnabled)
							info = dataAdapter.SelectCommand.GetInfo();
					}
					catch (Exception ex)
					{
						throw new RepositoryOperationException($"Could not perform SEARCH command [{typeof(T)}]", dataAdapter.SelectCommand.GetInfo(), ex);
					}
				}

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"SQL: Perform SEARCH command successful [{typeof(T)}] @ {dataSource.Name}",
						$"Query: {query}",
						$"Total of results: {identities.Count} - Page number: {pageNumber} - Page size: {pageSize} - Execution times: {stopwatch.GetElapsedTimes()}",
						info
					});

				return identities;
			}
		}
		#endregion

		#region Count (searching)
		static Tuple<string, List<DbParameter>> PrepareCount<T>(this DbProviderFactory dbProviderFactory, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null, string searchInColumns = "*") where T : class
		{
			// prepare
			var definition = RepositoryMediator.GetEntityDefinition<T>();

			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID, definition, true);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var statementsInfo = RepositoryExtensions.PrepareSqlStatements(filter, null, businessRepositoryEntityID, false, definition, null, propertiesInfo);

			// tables (FROM)
			var tables = $" FROM {definition.TableName} AS Origin"
				+ (extendedProperties != null ? $" LEFT JOIN {definition.RepositoryDefinition.ExtendedPropertiesTableName} AS Extent ON Origin.{definition.PrimaryKey}=Extent.ID" : "");

			// filtering expressions (WHERE)
			string where = statementsInfo.Item1 != null && !string.IsNullOrWhiteSpace(statementsInfo.Item1.Item1)
				? " WHERE " + statementsInfo.Item1.Item1
				: "";

			// searching terms
			var searchTerms = dbProviderFactory.GetSearchTerms(new SearchQuery(query));

			// SQL Server
			if (dbProviderFactory.IsSQLServer())
				tables += $" INNER JOIN CONTAINSTABLE ({definition.TableName}, {searchInColumns}, {searchTerms}) AS Search ON Origin.{definition.PrimaryKey}=Search.[KEY]";

			// MySQL
			else if (dbProviderFactory.IsMySQL())
			{
				searchInColumns = !searchInColumns.Equals("*")
					? searchInColumns
					: standardProperties
						.Where(attribute => attribute.Value.IsSearchable())
						.Select(attribute => $"Origin.{attribute.Value.Name}")
						.Join(", ");
				where += (!where.Equals("") ? " AND " : " WHERE ") + $"(MATCH({searchInColumns}) AGAINST ({searchTerms} IN BOOLEAN MODE) > 0";
			}

			// PostgreSQL
			else if (dbProviderFactory.IsPostgreSQL())
			{
				searchInColumns = !searchInColumns.Equals("*")
					? searchInColumns
					: standardProperties
						.Where(attribute => attribute.Value.IsSearchable())
						.Select(attribute => "Origin." + attribute.Value.Name)
						.Join(" || ");

				where += (!where.Equals("") ? " AND " : " WHERE ") + $"to_tsvector('english', {searchInColumns} || ' ') @@ to_tsquery('{searchTerms.Replace("'", "''")}')";
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = dbProviderFactory.CreateConnection(dataSource))
			{
				var command = connection.CreateCommand(dbProviderFactory.PrepareCount(query, filter, businessRepositoryEntityID));
				try
				{
					var total = command.ExecuteScalar().CastAs<long>();

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform COUNT command successful [{typeof(T)}] @ {dataSource.Name}",
							$"Total: {total} - Execution times: {stopwatch.GetElapsedTimes()}",
							command.GetInfo()
						});

					return total;
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform COUNT command [{typeof(T)}]", command.GetInfo(), ex);
				}
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			dataSource = dataSource ?? context.GetPrimaryDataSource();
			var dbProviderFactory = dataSource.GetProviderFactory();
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var command = connection.CreateCommand(dbProviderFactory.PrepareCount(query, filter, businessRepositoryEntityID));
				try
				{
					var total = (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>();

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs(new[]
						{
							$"SQL: Perform COUNT command successful [{typeof(T)}] @ {dataSource.Name}",
							$"Total: {total} - Execution times: {stopwatch.GetElapsedTimes()}",
							command.GetInfo()
						});

					return total;
				}
				catch (Exception ex)
				{
					throw new RepositoryOperationException($"Could not perform COUNT command [{typeof(T)}]", command.GetInfo(), ex);
				}
			}
		}
		#endregion

		#region Schemas & Indexes
		internal static async Task CreateTableAsync(this RepositoryContext context, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			// prepare
			var dbProviderFactory = dataSource.GetProviderFactory();
			var dbProviderFactoryName = dbProviderFactory.GetName();
			var sql = "";
			switch (dbProviderFactoryName)
			{
				case "SQLServer":
					sql = $"CREATE TABLE [{context.EntityDefinition.TableName}] ("
						+ context.EntityDefinition.Attributes.Where(attribute => !attribute.IsMappings()).Select(attribute => "[" + (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + "] " + attribute.GetDbTypeString(dbProviderFactory) + " " + (attribute.NotNull ? "NOT " : "") + "NULL").Join(", ")
						+ $", CONSTRAINT [PK_{context.EntityDefinition.TableName}] PRIMARY KEY CLUSTERED ([{context.EntityDefinition.PrimaryKey}] ASC) "
						+ "WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, IGNORE_DUP_KEY=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=ON) ON [PRIMARY]) ON [PRIMARY]";
					break;

				case "MySQL":
				case "PostgreSQL":
					sql = $"CREATE TABLE {context.EntityDefinition.TableName} ("
						+ context.EntityDefinition.Attributes.Where(attribute => !attribute.IsMappings()).Select(attribute => (string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column) + " " + attribute.GetDbTypeString(dbProviderFactory) + " " + (attribute.NotNull ? "NOT " : "") + "NULL").Join(", ")
						+ $", PRIMARY KEY ({context.EntityDefinition.PrimaryKey}))";
					break;
			}

			// create tables
			if (!sql.Equals(""))
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					try
					{
						var command = connection.CreateCommand(sql);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						tracker?.Invoke($"Create SQL table successful [{context.EntityDefinition.TableName}] @ {dataSource.Name}\r\nSQL Command: {sql}", null);
						if (tracker == null && RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new[]
							{
								$"STARTER: Create SQL table successful [{context.EntityDefinition.TableName}] @ {dataSource.Name}",
								$"SQL Command: {sql}"
							});
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs(new[]
						{
							$"STARTER: Error occurred while creating new SQL table [{context.EntityDefinition.TableName}] @ {dataSource.Name} => {ex.Message}",
							$"SQL Command: {sql}"
						}, ex, LogLevel.Error);
						throw new RepositoryOperationException("Error occurred while creating new SQL table", sql, ex);
					}
				}
		}

		internal static async Task CreateTableIndexesAsync(this RepositoryContext context, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			// prepare
			var prefix = $"IDX_{context.EntityDefinition.TableName}";
			var normalIndexes = new Dictionary<string, List<AttributeInfo>>(StringComparer.OrdinalIgnoreCase)
			{
				{ prefix, new List<AttributeInfo>() }
			};
			var uniqueIndexes = new Dictionary<string, List<AttributeInfo>>(StringComparer.OrdinalIgnoreCase);

			// sortables
			context.EntityDefinition.Attributes.Where(attribute => attribute.IsSortable() && !attribute.IsMappings()).ForEach(attribute =>
			{
				var sortInfo = attribute.GetCustomAttribute<SortableAttribute>();
				if (!string.IsNullOrWhiteSpace(sortInfo.UniqueIndexName))
				{
					var name = $"{prefix}_{sortInfo.UniqueIndexName}";
					if (uniqueIndexes.TryGetValue(name, out var indexes))
						indexes.Add(attribute);
					else
						uniqueIndexes.Add(name, new List<AttributeInfo> { attribute });

					if (!string.IsNullOrWhiteSpace(sortInfo.IndexName))
					{
						name = $"{prefix}_{sortInfo.IndexName}";
						if (normalIndexes.TryGetValue(name, out indexes))
							indexes.Add(attribute);
						else
							normalIndexes.Add(name, new List<AttributeInfo> { attribute });
					}
				}
				else
				{
					var name = $"{prefix}_{attribute.Name}_Mappings";
					if (normalIndexes.TryGetValue(name, out var indexes))
						indexes.Add(attribute);
					else
						normalIndexes.Add(name, new List<AttributeInfo> { attribute });
				}
			});

			// alias
			context.EntityDefinition.Attributes.Where(attribute => attribute.IsAlias()).ForEach(attribute =>
			{
				var aliasProps = attribute.GetCustomAttribute<AliasAttribute>().Properties.ToHashSet(",", true);
				var index = new[] { attribute, context.EntityDefinition.Attributes.FirstOrDefault(attr => attr.Name.IsEquals("RepositoryEntityID")) }.Concat(context.EntityDefinition.Attributes.Where(attr => aliasProps.Contains(attr.Name))).ToList();
				var name = $"{prefix}_Alias";
				if (uniqueIndexes.TryGetValue(name, out var indexes))
					indexes = indexes.Concat(index).ToList();
				else
					uniqueIndexes.Add(name, index);
			});

			var dbProviderFactory = dataSource.GetProviderFactory();
			var dbProviderFactoryName = dbProviderFactory.GetName();
			var sql = "";
			switch (dbProviderFactoryName)
			{
				case "SQLServer":
					normalIndexes.Where(kvp => kvp.Value.Count > 0).ForEach(kvp =>
					{
						sql += (sql.Equals("") ? "" : ";\n")
							+ $"CREATE NONCLUSTERED INDEX [{kvp.Key}] ON [{context.EntityDefinition.TableName}] ("
							+ kvp.Value.Select(attribute => $"[{(string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column)}] ASC").Join(", ")
							+ ") WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY]";
					});
					uniqueIndexes.Where(kvp => kvp.Value.Count > 0).ForEach(kvp =>
					{
						sql += (sql.Equals("") ? "" : ";")
							+ $"CREATE UNIQUE NONCLUSTERED INDEX [{kvp.Key}] ON [{context.EntityDefinition.TableName}] ("
							+ kvp.Value.Select(attribute => $"[{(string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column)}] ASC").Join(", ")
							+ ") WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY]";
					});
					break;

				case "MySQL":
				case "PostgreSQL":
					normalIndexes.Where(kvp => kvp.Value.Count > 0).ForEach(kvp =>
					{
						sql += (sql.Equals("") ? "" : ";\n")
							+ $"CREATE INDEX {kvp.Key} ON {context.EntityDefinition.TableName} ("
							+ kvp.Value.Select(attribute => $"{(string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column)} ASC").Join(", ")
							+ ")";
					});
					uniqueIndexes.Where(kvp => kvp.Value.Count > 0).ForEach(kvp =>
					{
						sql += (sql.Equals("") ? "" : ";\n")
							+ $"CREATE UNIQUE INDEX {kvp.Key} ON {context.EntityDefinition.TableName} ("
							+ kvp.Value.Select(attribute => $"{(string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column)} ASC").Join(", ")
							+ ")";
					});
					break;
			}

			// create index
			if (!sql.Equals(""))
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					try
					{
						var command = connection.CreateCommand(sql);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						tracker?.Invoke($"Create indexes of SQL table successful [{context.EntityDefinition.TableName}] @ {dataSource.Name}\r\nSQL Command: {sql}", null);
						if (tracker == null && RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new[]
							{
								$"STARTER: Create indexes of SQL table successful [{context.EntityDefinition.TableName}] @ {dataSource.Name}",
								$"SQL Command: {sql}"
							});
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs(new[]
						{
							$"STARTER: Error occurred while creating new indexes of SQL table [{context.EntityDefinition.TableName}] @ {dataSource.Name} => {ex.Message}",
							$"SQL Command: {sql}"
						}, ex, LogLevel.Error);
						throw new RepositoryOperationException("Error occurred while creating new indexes of SQL table", sql, ex);
					}
				}
		}

		internal static async Task CreateTableFulltextIndexAsync(this RepositoryContext context, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			// prepare
			var columns = context.EntityDefinition.Searchable
				? context.EntityDefinition.Attributes.Where(attribute => attribute.IsSearchable()).Select(attribute => string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column).ToList()
				: new List<string>();

			var dbProviderFactory = dataSource.GetProviderFactory();
			var sql = "";

			// check to create default fulltext catalog on Microsoft SQL Server
			if (dbProviderFactory.IsSQLServer())
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					var command = connection.CreateCommand();
					try
					{
						command.CommandText = $"SELECT COUNT(name) FROM sys.fulltext_catalogs WHERE name='{connection.Database.Replace("'", "")}'";
						if ((await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<int>() < 1)
						{
							command.CommandText = $"CREATE FULLTEXT CATALOG [{connection.Database.Replace("'", "")}] WITH ACCENT_SENSITIVITY = OFF AS DEFAULT AUTHORIZATION [dbo]";
							await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs(new[]
						{
							$"STARTER: Error occurred while creating the default full-text catalog of Microsoft SQL Server => {ex.Message}",
							$"SQL Command: {sql}"
						}, ex, LogLevel.Error);
						throw new RepositoryOperationException("Error occurred while creating the default full-text catalog of Microsoft SQL Server", command.CommandText, ex);
					}
				}

			if (columns.Count > 0)
				switch (dbProviderFactory.GetName())
				{
					case "SQLServer":
						sql = $"CREATE FULLTEXT INDEX ON [{context.EntityDefinition.TableName}] ({columns.Join(", ")}) KEY INDEX [PK_{context.EntityDefinition.TableName}]";
						break;

					case "MySQL":
						sql = $"CREATE FULLTEXT INDEX FT_{context.EntityDefinition.TableName} ON {context.EntityDefinition.TableName} ({columns.Join(", ")})";
						break;

					case "PostgreSQL":
						sql = $"CREATE INDEX FT_{context.EntityDefinition.TableName} ON {context.EntityDefinition.TableName} USING GIN (to_tsvector('english', {columns.Join(" || ' ' || ")}))";
						break;
				}

			// create full-text index
			if (!sql.Equals(""))
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					try
					{
						var command = connection.CreateCommand(sql);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						tracker?.Invoke($"Create full-text indexes of SQL table successful [{context.EntityDefinition.TableName}] @ {dataSource.Name}\r\nSQL Command: {sql}", null);
						if (tracker == null && RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new[]
							{
								$"STARTER: Create full-text indexes of SQL table successful [{context.EntityDefinition.TableName}] @ {dataSource.Name}",
								$"SQL Command: {sql}"
							});
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs(new[]
						{
							$"STARTER: Error occurred while creating new full-text indexes of SQL table [{context.EntityDefinition.TableName}] @ {dataSource.Name} => {ex.Message}",
							$"SQL Command: {sql}"
						}, ex, LogLevel.Error);
						throw new RepositoryOperationException("Error occurred while creating new full-text indexes of SQL table", sql, ex);
					}
				}
		}

		static async Task CreateMapingTableAsync(this RepositoryContext context, DataSource dataSource, string tableName, string linkColumn, string mapColumn, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			var dbProviderFactory = dataSource.GetProviderFactory();
			var sql = "";
			switch (dbProviderFactory.GetName())
			{
				case "SQLServer":
					sql = $"CREATE TABLE [{tableName}] ("
						+ $"[{linkColumn}] {typeof(string).GetDbTypeString(dbProviderFactory, 32, true, false)} NOT  NULL, "
						+ $"[{mapColumn}] {typeof(string).GetDbTypeString(dbProviderFactory, 32, true, false)} NOT  NULL, "
						+ $"CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([{linkColumn}] ASC, [{mapColumn}] ASC) "
						+ "WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, IGNORE_DUP_KEY=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=ON) ON [PRIMARY]) ON [PRIMARY]";
					break;

				case "MySQL":
				case "PostgreSQL":
					sql = $"CREATE TABLE {tableName} ("
						+ $"{linkColumn} {typeof(string).GetDbTypeString(dbProviderFactory, 32, true, false)} NOT  NULL, "
						+ $"{mapColumn} {typeof(string).GetDbTypeString(dbProviderFactory, 32, true, false)} NOT  NULL, "
						+ $"PRIMARY KEY ({linkColumn} ASC, {mapColumn} ASC)"
						+ ")";
					break;
			}

			// create table
			if (!sql.Equals(""))
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					try
					{
						var command = connection.CreateCommand(sql);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						tracker?.Invoke($"Create SQL mapping table successful [{tableName}] @ {dataSource.Name}\r\nSQL Command: {sql}", null);
						if (tracker == null && RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new[]
							{
								$"STARTER: Create SQL mapping table successful [{tableName}] @ {dataSource.Name}",
								$"SQL Command: {sql}"
							});
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs(new[]
						{
							$"STARTER: Error occurred while creating new SQL mapping table [{tableName}] @ {dataSource.Name} => {ex.Message}",
							$"SQL Command: {sql}"
						}, ex, LogLevel.Error);
						throw new RepositoryOperationException("Error occurred while creating new SQL mapping table", sql, ex);
					}
				}
		}

		internal static async Task CreateExtentTableAsync(this RepositoryContext context, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			var dbProviderFactory = dataSource.GetProviderFactory();
			var dbProviderFactoryName = dbProviderFactory.GetName();
			var tableName = context.EntityDefinition.RepositoryDefinition.ExtendedPropertiesTableName;

			var sql = "";
			var isExisted = false;
			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				sql = dbProviderFactory.IsPostgreSQL()
					? $"SELECT COUNT(tablename) FROM pg_tables WHERE schemaname='public' AND tablename='{tableName.Replace("'", "''").ToLower()}'"
					: $"SELECT COUNT(table_name) FROM information_schema.tables WHERE table_name='{tableName.Replace("'", "''")}'";
				try
				{
					var command = connection.CreateCommand(sql);
					isExisted = (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>() > 0;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs(new[]
					{
						$"STARTER: Error occurred while checking existed of SQL table => {ex.Message}",
						$"SQL Command: {sql}"
					}, ex, LogLevel.Error);
					throw new RepositoryOperationException("Error occurred while checking existed of SQL table ", sql, ex);
				}
			}
			if (isExisted)
				return;

			var columns = new Dictionary<string, Tuple<Type, int>>
			{
				{ "ID", new Tuple<Type, int>(typeof(string), 32) },
				{ "SystemID", new Tuple<Type, int>(typeof(string), 32) },
				{ "RepositoryID", new Tuple<Type, int>(typeof(string), 32) },
				{ "RepositoryEntityID", new Tuple<Type, int>(typeof(string), 32) },
			};

			var max = dbProviderFactoryName.Equals("MySQL") ? 15 : 30;
			for (var index = 1; index <= max; index++)
				columns.Add($"SmallText{index}", new Tuple<Type, int>(typeof(string), 250));

			max = dbProviderFactoryName.Equals("MySQL") ? 5 : 10;
			for (var index = 1; index <= max; index++)
				columns.Add($"MediumText{index}", new Tuple<Type, int>(typeof(string), 4000));

			max = 5;
			for (var index = 1; index <= max; index++)
				columns.Add($"LargeText{index}", new Tuple<Type, int>(typeof(string), 0));

			max = dbProviderFactoryName.Equals("MySQL") ? 10 : 20;
			for (var index = 1; index <= max; index++)
				columns.Add($"YesNo{index}", new Tuple<Type, int>(typeof(bool), 0));

			max = dbProviderFactoryName.Equals("MySQL") ? 10 : 20;
			for (var index = 1; index <= max; index++)
				columns.Add($"DateTime{index}", new Tuple<Type, int>(typeof(DateTime), 0));

			max = dbProviderFactoryName.Equals("MySQL") ? 20 : 40;
			for (var index = 1; index <= max; index++)
				columns.Add($"IntegralNumber{index}", new Tuple<Type, int>(typeof(long), 0));

			max = 10;
			for (var index = 1; index <= max; index++)
				columns.Add($"FloatingPointNumber{index}", new Tuple<Type, int>(typeof(decimal), 0));

			switch (dbProviderFactoryName)
			{
				case "SQLServer":
					sql = $"CREATE TABLE [{tableName}] ("
						+ columns.Select(kvp =>
						{
							var type = kvp.Value.Item1;
							var precision = kvp.Value.Item2;
							var asFixedLength = type.Equals(typeof(string)) && precision.Equals(32);
							var asCLOB = type.Equals(typeof(string)) && precision.Equals(0);
							return $"[{kvp.Key}] "
								+ type.GetDbTypeString(dbProviderFactoryName, precision, asFixedLength, asCLOB)
								+ (kvp.Key.EndsWith("ID") ? " NOT" : "") + " NULL";
						}).Join(", ")
						+ $", CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([ID] ASC) "
						+ "WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, IGNORE_DUP_KEY=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=ON) ON [PRIMARY]) ON [PRIMARY];\n"
						+ $"CREATE NONCLUSTERED INDEX [IDX_{tableName}] ON [{tableName}] ([ID] ASC, [SystemID] ASC, [RepositoryID] ASC, [RepositoryEntityID] ASC)"
						+ " WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY];";
					columns.ForEach((kvp, index) =>
					{
						var type = kvp.Value.Item1;
						var precision = kvp.Value.Item2;
						var isBigText = type.Equals(typeof(string)) && precision.Equals(0);
						if (index > 3 && !isBigText)
							sql += "\n"
								+ $"CREATE NONCLUSTERED INDEX [IDX_{tableName}_{kvp.Key}] ON [{tableName}] ([{kvp.Key}] ASC)"
								+ " WITH (PAD_INDEX=OFF, STATISTICS_NORECOMPUTE=OFF, SORT_IN_TEMPDB=OFF, DROP_EXISTING=OFF, ONLINE=OFF, ALLOW_ROW_LOCKS=ON, ALLOW_PAGE_LOCKS=OFF) ON [PRIMARY];";
					});
					break;

				case "MySQL":
				case "PostgreSQL":
					sql = $"CREATE TABLE {tableName} ("
						+ columns.Select(kvp =>
						{
							var type = kvp.Value.Item1;
							var precision = kvp.Value.Item2;
							var asFixedLength = type.Equals(typeof(string)) && precision.Equals(32);
							var asCLOB = type.Equals(typeof(string)) && precision.Equals(0);
							return kvp.Key + " "
								+ type.GetDbTypeString(dbProviderFactoryName, precision, asFixedLength, asCLOB)
								+ (kvp.Key.EndsWith("ID") ? " NOT" : "") + " NULL";
						}).Join(", ")
						+ ", PRIMARY KEY (ID ASC));\n"
						+ $"CREATE INDEX IDX_{tableName} ON {tableName} (ID ASC, SystemID ASC, RepositoryID ASC, RepositoryEntityID ASC);";
					columns.ForEach((kvp, index) =>
					{
						var type = kvp.Value.Item1;
						var precision = kvp.Value.Item2;
						var isBigText = type.Equals(typeof(string)) && (precision.Equals(0) || precision.Equals(4000));
						if (index > 3 && !isBigText)
							sql += "\n" + $"CREATE INDEX IDX_{tableName}_{kvp.Key} ON {tableName} ({kvp.Key} ASC);";
					});
					break;
			}

			// create table
			if (!sql.Equals(""))
				using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
				{
					try
					{
						var command = connection.CreateCommand(sql);
						await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
						tracker?.Invoke($"Create SQL table of extended properties successful [{tableName}] @ {dataSource.Name}\r\nSQL Command: {sql}", null);
						if (tracker == null && RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new[]
							{
								$"STARTER: Create SQL table of extended properties successful [{context.EntityDefinition.TableName}] @ {dataSource.Name}",
								$"SQL Command: {sql}"
							});
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs(new[]
						{
							$"STARTER: Error occurred while creating new SQL table of extended properties [{context.EntityDefinition.TableName}] @ {dataSource.Name} => {ex.Message}",
							$"SQL Command: {sql}"
						}, ex, LogLevel.Error);
						throw new RepositoryOperationException("Error occurred while creating new SQL table of extended properties", sql, ex);
					}
				}
		}

		internal static async Task EnsureSchemasAsync(this EntityDefinition definition, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			// check existed
			var isExisted = true;
			var dbProviderFactory = dataSource.GetProviderFactory();

			using (var connection = await dbProviderFactory.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false))
			{
				var sql = dbProviderFactory.IsPostgreSQL()
					? $"SELECT COUNT(tablename) FROM pg_tables WHERE schemaname='public' AND tablename='{definition.TableName.Replace("'", "''").ToLower()}'"
					: $"SELECT COUNT(table_name) FROM information_schema.tables WHERE table_name='{definition.TableName.Replace("'", "''")}'";
				try
				{
					var command = connection.CreateCommand(sql);
					isExisted = (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).CastAs<long>() > 0;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs(new[]
					{
						$"STARTER: Error occurred while checking existed of SQL table => {ex.Message}",
						$"SQL Command: {sql}"
					}, ex, LogLevel.Error);
					throw new RepositoryOperationException("Error occurred while checking existed of SQL table ", sql, ex);
				}
			}

			if (!isExisted)
				using (var context = new RepositoryContext(false))
				{
					context.Operation = RepositoryOperation.Create;
					context.EntityDefinition = definition;

					await context.CreateTableAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);

					await context.CreateTableIndexesAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);

					if (definition.Searchable)
						await context.CreateTableFulltextIndexAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);

					await definition.Attributes.Where(attribute => attribute.IsMappings()).ForEachAsync(async (attribute, token) =>
					{
						var mapInfo = attribute.GetMapInfo(definition);
						await context.CreateMapingTableAsync(dataSource, mapInfo.Item1, mapInfo.Item2, mapInfo.Item3, tracker, token).ConfigureAwait(false);
					}, cancellationToken, true, false).ConfigureAwait(false);

					if (definition.Extendable && definition.RepositoryDefinition != null)
						await context.CreateExtentTableAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);
				}
		}
		#endregion

	}

	#region Providers of SQL databases
	/// <summary>
	/// Information of a database provider for working with DbProviderFactory (replacement of System.Data.Common.DbProviderFactories)
	/// </summary>
	public sealed class DbProvider
	{
		/// <summary>
		/// Gets the invariant name of the provider
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// Gets the type of the provider
		/// </summary>
		public Type Type { get; private set; }

		/// <summary>
		/// Gets the description of the provider
		/// </summary>
		public string Description { get; private set; }

		/// <summary>
		/// Gets the collection of all available providers
		/// </summary>
		public static Dictionary<string, DbProvider> DbProviders { get; private set; }

		internal static Dictionary<string, DbProvider> ConstructDbProviderFactories(List<XmlNode> nodes, Action<string, Exception> tracker = null)
		{
			DbProvider.DbProviders = DbProvider.DbProviders ?? new Dictionary<string, DbProvider>(StringComparer.OrdinalIgnoreCase);
			nodes?.ForEach(node =>
			{
				var providerName = node.Attributes["invariant"]?.Value ?? node.Attributes["name"]?.Value;
				var providerType = node.Attributes["type"]?.Value;
				Type type = null;
				if (!string.IsNullOrWhiteSpace(providerName) && !string.IsNullOrWhiteSpace(providerType) && !DbProvider.DbProviders.ContainsKey(providerName))
					try
					{
						type = Enyim.Caching.AssemblyLoader.GetType(providerType);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while constructing a SQL Provider [{providerName}] => {ex.Message}", ex);
					}

				if (type != null)
				{
					DbProvider.DbProviders[providerName] = new DbProvider
					{
						Name = providerName,
						Type = type,
						Description = node.Attributes["description"]?.Value ?? $"SQL Provider for {providerName}"
					};
					tracker?.Invoke($"Construct a SQL Provider [{providerName} => {providerType}]", null);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Construct a SQL Provider [{providerName} => {providerType}]", null);
				}
			});
			return DbProvider.DbProviders;
		}

		internal static Dictionary<string, DbProvider> ConstructDbProviderFactories(XmlNode config, Action<string, Exception> tracker = null)
			=> config != null
				? DbProvider.ConstructDbProviderFactories(config.SelectNodes("add") is XmlNodeList nodes ? nodes.ToList() : null, tracker)
				: throw new ArgumentException("The configuration is invalid", nameof(config));

		internal static Dictionary<string, DbProvider> ConstructDbProviderFactories(AppConfigurationSectionHandler config, Action<string, Exception> tracker = null)
			=> DbProvider.ConstructDbProviderFactories(config?.Section, tracker);

		/// <summary>
		/// Get an instance of a DbProvider for a specified provider name
		/// </summary>
		/// <param name="name">The invariant name of a SQL Provider</param>
		/// <returns></returns>
		public static DbProvider GetProvider(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("The invariant name is invalid", nameof(name));

			if (DbProvider.DbProviders == null)
				DbProvider.ConstructDbProviderFactories(ConfigurationManager.GetSection(UtilityService.GetAppSetting("Section:DbProviderFactories", "net.vieapps.dbproviders")) is AppConfigurationSectionHandler config ? config : ConfigurationManager.GetSection("dbProviderFactories") as AppConfigurationSectionHandler);

			return DbProvider.DbProviders.TryGetValue(name, out var provider) ? provider : null;
		}

		/// <summary>
		/// Gets the collection of all installed SQL Provider Factory
		/// </summary>
		public static Dictionary<string, DbProviderFactory> DbProviderFactories { get; } = new Dictionary<string, DbProviderFactory>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Get an instance of a DbProviderFactory for a specified provider name
		/// </summary>
		/// <param name="name">The invariant name of the SQL Provider Factory</param>
		/// <returns></returns>
		public static DbProviderFactory GetFactory(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("The invariant name is invalid", nameof(name));

			if (!DbProvider.DbProviderFactories.TryGetValue(name, out var dbProviderFactory))
				lock (DbProvider.DbProviderFactories)
				{
					if (!DbProvider.DbProviderFactories.TryGetValue(name, out dbProviderFactory))
					{
						var provider = DbProvider.GetProvider(name);
						if (provider == null)
							throw new NotImplementedException($"The SQL Provider Factory ({name}) is not found");
						if (provider.Type == null)
							throw new InformationInvalidException($"The SQL Provider Factory ({name}) is invalid");

						var field = provider.Type.GetField("Instance", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
						if (field != null && field.FieldType.IsSubclassOf(typeof(DbProviderFactory)))
						{
							var value = field.GetValue(null);
							if (value != null)
								DbProvider.DbProviderFactories[name] = dbProviderFactory = (DbProviderFactory)value;
						}
					}
				}

			return dbProviderFactory ?? throw new NotImplementedException($"The SQL Provider Factory ({name}) is not installed");
		}
	}
	#endregion

}