#region Related components
using System;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Dynamic;

using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Filtering expression for using with comparing operators
	/// </summary>
	[Serializable]
	public class FilterBy<T> : IFilterBy<T> where T : class
	{
		/// <summary>
		/// Initializes a new filtering expression
		/// </summary>
		/// <param name="operator"></param>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		public FilterBy(string attribute = null, CompareOperator @operator = CompareOperator.Equals, object value = null)
			: this(null, attribute, @operator, value) { }

		/// <summary>
		/// Initializes a filtering expression
		/// </summary>
		/// <param name="json"></param>
		/// <param name="operator"></param>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		public FilterBy(JObject json, string attribute = null, CompareOperator @operator = CompareOperator.Equals, object value = null)
		{
			this.Attribute = attribute;
			this.Operator = @operator;
			this.Value = value;
			this.Parse(json);
		}

		#region Properties
		/// <summary>
		/// Gets or sets the operator
		/// </summary>
		public CompareOperator Operator { get; set; }

		/// <summary>
		/// Gets or sets the attribute for comparing/filtering
		/// </summary>
		public string Attribute { get; set; }

		/// <summary>
		/// Gets or sets the attribute's value for comparing/filtering
		/// </summary>
		public object Value { get; set; }

		/// <summary>
		/// Gets the parent filtering expression
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore]
		public FilterBys<T> Parent { get; internal set; }
		#endregion

		#region Working with JSON
		/// <summary>
		/// Parses the JSON
		/// </summary>
		/// <param name="json"></param>
		public void Parse(JObject json)
		{
			if (json != null)
			{
				this.Attribute = json.Get<string>("Attribute");
				this.Operator = (json.Get<string>("Operator") ?? "Equals").TryToEnum(out CompareOperator @operator) ? @operator : CompareOperator.Equals;
				this.Value = (json["Value"] as JValue)?.Value;
			}
		}

		/// <summary>
		/// Converts to JSON
		/// </summary>
		/// <returns></returns>
		public JToken ToJson()
			=> new JObject
			{
				{ "Attribute", this.Attribute },
				{ "Operator", this.Operator.ToString() },
				{ "Value", new JValue(this.Value) }
			};

		public string ToString(Newtonsoft.Json.Formatting formatting)
			=> this.ToJson().ToString(formatting);

		public override string ToString()
			=> this.ToString(Newtonsoft.Json.Formatting.None);
		#endregion

		object GetValue(Dictionary<string, AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties)
		{
			try
			{
				if (this.Attribute == null || this.Value == null)
					return this.Value;

				else if (standardProperties != null && standardProperties.TryGetValue(this.Attribute, out AttributeInfo standardProperty) && standardProperty != null)
				{
					if (standardProperty.GetType().IsDateTimeType())
					{
						var value = this.Value.GetType().IsDateTimeType()
							? (DateTime)this.Value
							: DateTime.Parse(this.Value.ToString());
						return standardProperty.IsStoredAsString()
							? value.ToDTString() as object
							: value;
					}
					else
						return standardProperty.GetType().IsStringType()
							? this.Value.ToString()
							: this.Value;
				}

				else if (extendedProperties != null && extendedProperties.TryGetValue(this.Attribute, out ExtendedPropertyDefinition extendedProperty) && extendedProperty != null)
					switch (extendedProperty.Mode)
					{
						case ExtendedPropertyMode.SmallText:
						case ExtendedPropertyMode.MediumText:
						case ExtendedPropertyMode.LargeText:
						case ExtendedPropertyMode.Select:
						case ExtendedPropertyMode.Lookup:
						case ExtendedPropertyMode.User:
							return this.Value.ToString();

						case ExtendedPropertyMode.DateTime:
							return this.Value.GetType().IsDateTimeType()
								? ((DateTime)this.Value).ToDTString()
								: DateTime.Parse(this.Value.ToString()).ToDTString();

						default:
							return this.Value;
					}

				else
					return this.Value;
			}
			catch
			{
				return this.Value;
			}
		}

		#region Working with SQL
		internal Tuple<string, Dictionary<string, object>> GetSqlStatement(string surfix, Dictionary<string, AttributeInfo> standardProperties = null, Dictionary<string, ExtendedPropertyDefinition> extendedProperties = null, EntityDefinition definition = null, List<string> parentIDs = null)
		{
			if (string.IsNullOrEmpty(this.Attribute))
				return null;

			var statement = "";
			var parameters = new Dictionary<string, object>();

			if (definition != null && definition.ParentType != null && definition.ParentAssociatedProperty.Equals(this.Attribute)
				&& definition.MultipleParentAssociates && !string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesMapColumn) && !string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesLinkColumn)
				&& parentIDs != null && parentIDs.Count > 0 && this.Operator.Equals(CompareOperator.Equals))
			{
				parentIDs.ForEach((id, index) =>
				{
					var surf = (!string.IsNullOrEmpty(surfix) ? surfix : "") + "_" + index.ToString();
					statement += (statement.Equals("") ? "" : " OR ")
						+ $"Origin.{this.Attribute}=@{this.Attribute + surf}"
						+ $" OR Link.{definition.MultipleParentAssociatesMapColumn}=@{this.Attribute + surf}_L";

					parameters.Add("@" + this.Attribute + surf, id);
					parameters.Add("@" + this.Attribute + surf + "_L", id);
				});
				statement = "(" + statement + ")";
			}

			else
			{
				var column = extendedProperties != null && extendedProperties.ContainsKey(this.Attribute)
					? extendedProperties[this.Attribute].Column
					: standardProperties != null && standardProperties.ContainsKey(this.Attribute)
						? !string.IsNullOrWhiteSpace(standardProperties[this.Attribute].Column)
							? standardProperties[this.Attribute].Column
							: standardProperties[this.Attribute].Name
						: this.Attribute;

				var name = this.Attribute + (!string.IsNullOrEmpty(surfix) ? surfix : "");

				var @operator = "=";
				var value = this.GetValue(standardProperties, extendedProperties);

				var gotName = true;
				switch (this.Operator)
				{
					case CompareOperator.Equals:
						@operator = "=";
						break;

					case CompareOperator.NotEquals:
						@operator = "<>";
						break;

					case CompareOperator.LessThan:
						@operator = "<";
						break;

					case CompareOperator.LessThanOrEquals:
						@operator = "<=";
						break;

					case CompareOperator.Greater:
						@operator = ">";
						break;

					case CompareOperator.GreaterOrEquals:
						@operator = ">=";
						break;

					case CompareOperator.Contains:
						@operator = $"LIKE '%@{name}%'";
						gotName = false;
						break;

					case CompareOperator.StartsWith:
						@operator = $"LIKE '@{name}%'";
						gotName = false;
						break;

					case CompareOperator.EndsWith:
						@operator = $"LIKE '%@{name}'";
						gotName = false;
						break;

					case CompareOperator.IsNull:
						@operator = "IS NULL";
						value = null;
						gotName = false;
						break;

					case CompareOperator.IsNotNull:
						@operator = "IS NOT NULL";
						gotName = false;
						value = null;
						break;

					case CompareOperator.IsEmpty:
						@operator = "=";
						value = "";
						break;

					case CompareOperator.IsNotEmpty:
						@operator = "<>";
						value = "";
						break;

					default:
						break;
				}

				statement = (extendedProperties != null && extendedProperties.ContainsKey(this.Attribute) ? "Extent" : "Origin") + "." + column
					+ @operator + (gotName ? $"@{name}" : "");

				if (value != null)
					parameters.Add("@" + name, value);
			}

			return new Tuple<string, Dictionary<string, object>>(statement, parameters);
		}

		public Tuple<string, Dictionary<string, object>> GetSqlStatement()
			=> this.GetSqlStatement(null);
		#endregion

		#region Working with No SQL
		internal FilterDefinition<T> GetNoSqlStatement(Dictionary<string, AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties, EntityDefinition definition = null, List<string> parentIDs = null)
		{
			if (string.IsNullOrWhiteSpace(this.Attribute))
				return null;

			var attribute = extendedProperties != null && extendedProperties.ContainsKey(this.Attribute)
				? "ExtendedProperties." + this.Attribute
				: this.Attribute;

			FilterDefinition<T> filter = null;

			if (definition != null && definition.ParentType != null && definition.ParentAssociatedProperty.Equals(attribute)
				&& definition.MultipleParentAssociates && !string.IsNullOrWhiteSpace(definition.MultipleParentAssociatesProperty) && parentIDs != null
				&& this.Operator.Equals(CompareOperator.Equals))
				parentIDs.ForEach(id =>
				{
					var filterBy = Builders<T>.Filter.Eq(definition.ParentAssociatedProperty, id) | Builders<T>.Filter.Eq(definition.MultipleParentAssociatesProperty, id);
					filter = filter == null
						? filterBy
						: filter | filterBy;
				});

			else
			{
				var value = this.GetValue(standardProperties, extendedProperties);
				switch (this.Operator)
				{
					case CompareOperator.Equals:
						filter = Builders<T>.Filter.Eq(attribute, value);
						break;

					case CompareOperator.NotEquals:
						filter = Builders<T>.Filter.Ne(attribute, value);
						break;

					case CompareOperator.LessThan:
						filter = Builders<T>.Filter.Lt(attribute, value);
						break;

					case CompareOperator.LessThanOrEquals:
						filter = Builders<T>.Filter.Lte(attribute, value);
						break;

					case CompareOperator.Greater:
						filter = Builders<T>.Filter.Gt(attribute, value);
						break;

					case CompareOperator.GreaterOrEquals:
						filter = Builders<T>.Filter.Gte(attribute, value);
						break;

					case CompareOperator.Contains:
						filter = value == null || !value.GetType().IsStringType() || value.Equals("")
							? Builders<T>.Filter.Eq(attribute, "")
							: Builders<T>.Filter.Regex(attribute, new BsonRegularExpression($"/.*{value}.*/"));
						break;

					case CompareOperator.StartsWith:
						filter = value == null || !value.GetType().IsStringType() || value.Equals("")
							? Builders<T>.Filter.Eq(attribute, "")
							: Builders<T>.Filter.Regex(attribute, new BsonRegularExpression($"^{value}"));
						break;

					case CompareOperator.EndsWith:
						filter = value == null || !value.GetType().IsStringType() || value.Equals("")
							? Builders<T>.Filter.Eq(attribute, "")
							: Builders<T>.Filter.Regex(attribute, new BsonRegularExpression($"{value}$"));
						break;

					case CompareOperator.IsNull:
					case CompareOperator.IsNotNull:
						var type = standardProperties.TryGetValue(this.Attribute, out AttributeInfo standardAttribute)
							? standardAttribute.Type
							: extendedProperties.TryGetValue(this.Attribute, out ExtendedPropertyDefinition extendedAttribute)
								? extendedAttribute.Type
								: null;
						if (this.Operator == CompareOperator.IsNull)
						{
							if (type != null && type.IsStringType())
								filter = Builders<T>.Filter.Eq<string>(attribute, null);
							else
								filter = Builders<T>.Filter.Eq(attribute, BsonNull.Value);
						}
						else
						{
							if (type != null && type.IsStringType())
								filter = Builders<T>.Filter.Ne<string>(attribute, null);
							else
								filter = Builders<T>.Filter.Ne(attribute, BsonNull.Value);
						}
						break;

					case CompareOperator.IsEmpty:
						filter = Builders<T>.Filter.Eq(attribute, "");
						break;

					case CompareOperator.IsNotEmpty:
						filter = Builders<T>.Filter.Ne(attribute, "");
						break;

					default:
						break;
				}
			}

			return filter;
		}

		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		public FilterDefinition<T> GetNoSqlStatement()
			=> this.GetNoSqlStatement(null, null);
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// Filtering expression for using with combining operators (AND/OR)
	/// </summary>
	[Serializable]
	public class FilterBys<T> : IFilterBy<T> where T : class
	{
		/// <summary>
		/// Initializes a group of filtering expression
		/// </summary>
		/// <param name="operator"></param>
		/// <param name="children"></param>
		public FilterBys(GroupOperator @operator = GroupOperator.And, List<IFilterBy<T>> children = null)
			: this(null, @operator, children) { }

		/// <summary>
		/// Initializes a group of filtering expression
		/// </summary>
		/// <param name="json"></param>
		/// <param name="operator"></param>
		/// <param name="children"></param>
		public FilterBys(JObject json, GroupOperator @operator = GroupOperator.And, List<IFilterBy<T>> children = null)
		{
			this.Operator = @operator;
			this.Children = children ?? new List<IFilterBy<T>>();
			this.Parse(json);
		}

		#region Properties
		/// <summary>
		/// Gets or sets the operator
		/// </summary>
		public GroupOperator Operator { get; set; }

		/// <summary>
		/// Gets the parent filtering expression
		/// </summary>
		[JsonIgnore, XmlIgnore]
		public FilterBys<T> Parent { get; internal set; }

		/// <summary>
		/// Gets the collection of children
		/// </summary>
		public List<IFilterBy<T>> Children { get; internal set; }
		#endregion

		/// <summary>
		/// Adds a filtering expression into the collection of children
		/// </summary>
		/// <param name="filter"></param>
		public void Add(IFilterBy<T> filter)
		{
			if (filter != null)
				this.Children.Add(filter);
		}

		#region Working with JSON
		/// <summary>
		/// Parses the JSON
		/// </summary>
		/// <param name="json"></param>
		public void Parse(JObject json)
		{
			if (json != null)
			{
				this.Operator = (json.Get<string>("Operator") ?? "And").TryToEnum(out GroupOperator op) ? op : GroupOperator.And;
				json.Get<JArray>("Children")?.ForEach(cjson =>
				{
					var @operator = cjson.Get<string>("Operator");
					if (!string.IsNullOrWhiteSpace(@operator))
						this.Add(@operator.IsEquals("And") || @operator.IsEquals("Or") ? new FilterBys<T>(cjson as JObject) as IFilterBy<T> : new FilterBy<T>(cjson as JObject) as IFilterBy<T>);
				});
			}
		}

		/// <summary>
		/// Converts to JSON
		/// </summary>
		/// <returns></returns>
		public JToken ToJson()
			=> new JObject
			{
				{ "Operator", this.Operator.ToString() },
				{ "Children", this.Children.Select(c => c.ToJson()).ToJArray() }
			};

		public string ToString(Newtonsoft.Json.Formatting formatting)
			=> this.ToJson().ToString(formatting);

		public override string ToString()
			=> this.ToString(Newtonsoft.Json.Formatting.None);
		#endregion

		#region Working with statement of SQL
		internal Tuple<string, Dictionary<string, object>> GetSqlStatement(string surfix, Dictionary<string, AttributeInfo> standardProperties = null, Dictionary<string, ExtendedPropertyDefinition> extendedProperties = null, EntityDefinition definition = null, List<string> parentIDs = null)
		{
			if (this.Children == null || this.Children.Count < 1)
				return null;

			else if (this.Children.Count.Equals(1))
				return this.Children[0] is FilterBys<T>
					? (this.Children[0] as FilterBys<T>).GetSqlStatement(surfix, standardProperties, extendedProperties, definition, parentIDs)
					: (this.Children[0] as FilterBy<T>).GetSqlStatement(surfix, standardProperties, extendedProperties, definition, parentIDs);

			else
			{
				var statement = "";
				var parameters = new Dictionary<string, object>();
				this.Children.ForEach((child, index) =>
				{
					var data = child is FilterBys<T>
						? (child as FilterBys<T>).GetSqlStatement((!string.IsNullOrEmpty(surfix) ? surfix : "") + "_" + index.ToString(), standardProperties, extendedProperties, definition, parentIDs)
						: (child as FilterBy<T>).GetSqlStatement((!string.IsNullOrEmpty(surfix) ? surfix : "") + "_" + index.ToString(), standardProperties, extendedProperties, definition, parentIDs);

					if (data != null)
					{
						statement += (statement.Equals("") ? "" : this.Operator.Equals(GroupOperator.And) ? " AND " : " OR ") + data.Item1;
						data.Item2.ForEach(parameter => parameters.Add(parameter.Key, parameter.Value));
					}
				});

				return !statement.Equals("") && parameters.Count > 0
					? new Tuple<string, Dictionary<string, object>>((!string.IsNullOrEmpty(surfix) ? "(" : "") + statement + (!string.IsNullOrEmpty(surfix) ? ")" : ""), parameters)
					: null;
			}
		}

		public Tuple<string, Dictionary<string, object>> GetSqlStatement()
			=> this.GetSqlStatement(null);
		#endregion

		#region Working with statement of No SQL
		internal FilterDefinition<T> GetNoSqlStatement(Dictionary<string, AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties, EntityDefinition definition = null, List<string> parentIDs = null)
		{
			FilterDefinition<T> filter = null;

			if (this.Children == null || this.Children.Count < 1)
				filter = Builders<T>.Filter.Empty;

			else if (this.Children.Count.Equals(1))
				filter = this.Children[0] is FilterBys<T>
					? (this.Children[0] as FilterBys<T>).GetNoSqlStatement(standardProperties, extendedProperties, definition, parentIDs)
					: (this.Children[0] as FilterBy<T>).GetNoSqlStatement(standardProperties, extendedProperties, definition, parentIDs);

			else
				this.Children.ForEach(child =>
				{
					var childFilter = child is FilterBys<T>
						? (child as FilterBys<T>).GetNoSqlStatement(standardProperties, extendedProperties, definition, parentIDs)
						: (child as FilterBy<T>).GetNoSqlStatement(standardProperties, extendedProperties, definition, parentIDs);

					if (childFilter != null)
						filter = filter == null
							? childFilter
							: this.Operator.Equals(GroupOperator.And)
								? filter & childFilter
								: filter | childFilter;
				});

			return filter;
		}

		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		public FilterDefinition<T> GetNoSqlStatement()
			=> this.GetNoSqlStatement(null, null);
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// Wrapper of all available filtering expressions
	/// </summary>
	public static class Filters<T> where T : class
	{

		#region Group
		/// <summary>
		/// Creates a group of filter-by expressions with AND operator
		/// </summary>
		/// <param name="filters"></param>
		/// <returns></returns>
		public static FilterBys<T> And(params IFilterBy<T>[] filters)
		{
			var filter = new FilterBys<T>(GroupOperator.And);
			filters?.ForEach(item => filter.Add(item));
			return filter;
		}

		/// <summary>
		/// Creates a group of filter-by expressions with AND operator
		/// </summary>
		/// <param name="filters"></param>
		/// <returns></returns>
		public static FilterBys<T> And(IEnumerable<IFilterBy<T>> filters)
		{
			return new FilterBys<T>(GroupOperator.And, filters?.ToList());
		}

		/// <summary>
		/// Creates a group of filter-by expressions with OR operator
		/// </summary>
		/// <param name="filters"></param>
		/// <returns></returns>
		public static FilterBys<T> Or(params IFilterBy<T>[] filters)
		{
			var filter = new FilterBys<T>(GroupOperator.Or);
			filters?.ForEach(item => filter.Add(item));
			return filter;
		}

		/// <summary>
		/// Creates a group of filter-by expressions with OR operator
		/// </summary>
		/// <param name="filters"></param>
		/// <returns></returns>
		public static FilterBys<T> Or(IEnumerable<IFilterBy<T>> filters)
			=> new FilterBys<T>(GroupOperator.Or, filters?.ToList());
		#endregion

		#region Compare
		/// <summary>
		/// Creates a filter-by expressions with EQUALS operator (==)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Equals(string attribute, object value)
			=> new FilterBy<T>(attribute, CompareOperator.Equals, value);

		/// <summary>
		/// Creates a filter-by expressions with NOT-EQUALS operator (!=)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> NotEquals(string attribute, object value)
			=> new FilterBy<T>(attribute, CompareOperator.NotEquals, value);

		/// <summary>
		/// Creates a filter-by expressions with LESS THAN operator (&lt;)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> LessThan(string attribute, object value)
			=> new FilterBy<T>(attribute, CompareOperator.LessThan, value);

		/// <summary>
		/// Creates a filter-by expressions with LESS THAN or EQUALS operator (&lt;=)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> LessThanOrEquals(string attribute, object value)
			=> new FilterBy<T>(attribute, CompareOperator.LessThanOrEquals, value);

		/// <summary>
		/// Creates a filter-by expressions with GREATER operator (&gt;)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Greater(string attribute, object value)
			=> new FilterBy<T>(attribute, CompareOperator.Greater, value);

		/// <summary>
		/// Creates a filter-by expressions with GREATER or EQUALS operator (&gt;=)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> GreaterOrEquals(string attribute, object value)
			=> new FilterBy<T>(attribute, CompareOperator.GreaterOrEquals, value);

		/// <summary>
		/// Creates a filter-by expressions with CONTAINS operator (means the value of attribute must contains the sub-string)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Contains(string attribute, object value)
			=> new FilterBy<T>(attribute, CompareOperator.Contains, value);

		/// <summary>
		/// Creates a filter-by expressions with STARTS WITH operator (means the value of attribute must starts with the sub-string)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> StartsWith(string attribute, string value)
			=> new FilterBy<T>(attribute, CompareOperator.StartsWith, value);

		/// <summary>
		/// Creates a filter-by expressions with ENDS WITH operator (means the value of attribute must ends with the sub-string)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> EndsWith(string attribute, string value)
			=> new FilterBy<T>(attribute, CompareOperator.EndsWith, value);

		/// <summary>
		/// Creates a filter-by expressions with IS NULL operator (IS NULL)
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNull(string attribute)
			=> new FilterBy<T>(attribute, CompareOperator.IsNull, null);

		/// <summary>
		/// Creates a filter-by expressions with IS NOT NULL operator (IS NOT NULL)
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNotNull(string attribute)
			=> new FilterBy<T>(attribute, CompareOperator.IsNotNull, null);

		/// <summary>
		/// Creates a filter-by expressions with IS EMPTY operator (=='')
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsEmpty(string attribute)
			=> new FilterBy<T>(attribute, CompareOperator.IsEmpty, null);

		/// <summary>
		/// Creates a filter-by expressions with IS NOT EMPTY operator (!='')
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNotEmpty(string attribute)
			=> new FilterBy<T>(attribute, CompareOperator.IsNotEmpty, null);
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// Sorting expression
	/// </summary>
	[Serializable]
	public class SortBy<T> where T : class
	{
		/// <summary>
		/// Initializes a sorting expression
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="mode"></param>
		public SortBy(string attribute = null, SortMode mode = SortMode.Ascending)
			: this(null, attribute, mode) { }

		/// <summary>
		/// Initializes a sorting expression
		/// </summary>
		/// <param name="json"></param>
		/// <param name="attribute"></param>
		/// <param name="mode"></param>
		public SortBy(JObject json, string attribute = null, SortMode mode = SortMode.Ascending)
		{
			this.Attribute = attribute;
			this.Mode = mode;
			this.Parse(json);
		}

		#region Properties
		/// <summary>
		/// Gets or sets the attribute for sorting
		/// </summary>
		public string Attribute { get; set; }

		/// <summary>
		/// Gets or sets the mode for sorting
		/// </summary>
		public SortMode Mode { get; set; }

		/// <summary>
		/// Gets or sets the next-sibling
		/// </summary>
		public SortBy<T> ThenBy { get; set; }
		#endregion

		#region Working with JSON
		/// <summary>
		/// Parses from JSON
		/// </summary>
		/// <param name="json"></param>
		public void Parse(JObject json)
		{
			if (json != null)
			{
				this.Attribute = json.Get<string>("Attribute");
				this.Mode = (json.Get<string>("Mode") ?? "Ascending").ToEnum<SortMode>();
				var thenSortBy = json.Get<JObject>("ThenBy");
				this.ThenBy = thenSortBy != null
					? new SortBy<T>(thenSortBy)
					: null;
			}
		}

		/// <summary>
		/// Converts to JSON
		/// </summary>
		/// <returns></returns>
		public JObject ToJson()
			=> new JObject
			{
				{ "Attribute", this.Attribute },
				{ "Mode", this.Mode.ToString() },
				{ "ThenBy", this.ThenBy?.ToJson() }
			};

		public string ToString(Newtonsoft.Json.Formatting formatting)
			=> this.ToJson().ToString(formatting);

		public override string ToString()
			=> this.ToString(Newtonsoft.Json.Formatting.None);
		#endregion

		#region Working with statement of SQL
		internal string GetSqlStatement(Dictionary<string, AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties)
		{
			if (string.IsNullOrWhiteSpace(this.Attribute))
				return null;

			var next = this.ThenBy?.GetSqlStatement(standardProperties, extendedProperties);
			return this.Attribute + (this.Mode.Equals(SortMode.Ascending) ? " ASC" : " DESC") + (!string.IsNullOrWhiteSpace(next) ? ", " + next : "");
		}

		/// <summary>
		/// Gets the statement of SQL
		/// </summary>
		/// <returns></returns>
		public SortDefinition<T> GetSqlStatement()
			=> this.GetSqlStatement(null, null);
		#endregion

		#region Working with statement of No SQL
		internal SortDefinition<T> GetNoSqlStatement(SortDefinition<T> previous, Dictionary<string, AttributeInfo> standardProperties = null, Dictionary<string, ExtendedPropertyDefinition> extendedProperties = null)
		{
			if (string.IsNullOrWhiteSpace(this.Attribute))
				return null;

			var attribute = extendedProperties != null && extendedProperties.ContainsKey(this.Attribute)
				? "ExtendedProperties." + this.Attribute
				: this.Attribute;

			var sort = previous != null
				? this.Mode.Equals(SortMode.Ascending)
					? previous.Ascending(attribute)
					: previous.Descending(attribute)
				: this.Mode.Equals(SortMode.Ascending)
					? Builders<T>.Sort.Ascending(attribute)
					: Builders<T>.Sort.Descending(attribute);

			return this.ThenBy != null
				? this.ThenBy.GetNoSqlStatement(sort, standardProperties, extendedProperties)
				: sort;
		}

		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		public SortDefinition<T> GetNoSqlStatement()
			=> this.GetNoSqlStatement(null);
		#endregion

		internal List<string> GetAttributes()
			=> new[] { this.Attribute }.Concat(this.ThenBy?.GetAttributes() ?? new List<string>()).ToList();
	}

	// ------------------------------------------

	/// <summary>
	/// Wrapper of all available sorting expressions
	/// </summary>
	public static class Sorts<T> where T : class
	{
		/// <summary>
		/// Creates an ascending sort
		/// </summary>
		public static SortBy<T> Ascending(string attribute)
			=> new SortBy<T>(attribute, SortMode.Ascending);

		/// <summary>
		/// Creates a descending sort
		/// </summary>
		public static SortBy<T> Descending(string attribute)
			=> new SortBy<T>(attribute, SortMode.Descending);
	}

	// ------------------------------------------

	/// <summary>
	/// Extension methods for working with repository
	/// </summary>
	public static partial class Extensions
	{

		#region ThenBy
		/// <summary>
		/// Creates a combined sorting expression with an ascending sort
		/// </summary>
		public static SortBy<T> ThenByAscending<T>(this SortBy<T> sort, string attribute) where T : class
		{
			sort.ThenBy = new SortBy<T>(attribute, SortMode.Ascending);
			return sort;
		}

		/// <summary>
		/// Creates a combined sorting expression with a descending sort
		/// </summary>
		public static SortBy<T> ThenByDescending<T>(this SortBy<T> sort, string attribute) where T : class
		{
			sort.ThenBy = new SortBy<T>(attribute, SortMode.Descending);
			return sort;
		}
		#endregion

		#region Statements of SQL
		internal static Tuple<Tuple<string, Dictionary<string, object>>, string> PrepareSqlStatements<T>(IFilterBy<T> filter, SortBy<T> sort, string businessEntityID, bool autoAssociateWithMultipleParents, EntityDefinition definition = null, List<string> parentIDs = null, Tuple<Dictionary<string, AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> propertiesInfo = null) where T : class
		{
			definition = definition ?? RepositoryMediator.GetEntityDefinition<T>();
			propertiesInfo = propertiesInfo ?? RepositoryMediator.GetProperties<T>(businessEntityID, definition);
			parentIDs = parentIDs ?? (definition != null && autoAssociateWithMultipleParents && filter != null ? filter.GetAssociatedParentIDs(definition) : null);

			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var filterBy = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetSqlStatement(null, standardProperties, extendedProperties, definition, parentIDs)
					: (filter as FilterBy<T>).GetSqlStatement(null, standardProperties, extendedProperties, definition, parentIDs)
				: null;

			if (!string.IsNullOrWhiteSpace(businessEntityID) && extendedProperties != null)
				filterBy = new Tuple<string, Dictionary<string, object>>
				(
					"Origin.EntityID=@EntityID" + (filterBy != null ? " AND " + filterBy.Item1 : ""),
					new Dictionary<string, object>(filterBy != null ? filterBy.Item2 : new Dictionary<string, object>())
					{
						{ "@EntityID", businessEntityID }
					}
				);

			var sortBy = sort?.GetSqlStatement(standardProperties, extendedProperties);

			return new Tuple<Tuple<string, Dictionary<string, object>>, string>(filterBy, sortBy);
		}
		#endregion

		#region Statements of No SQL
		internal static Tuple<FilterDefinition<T>, SortDefinition<T>> PrepareNoSqlStatements<T>(IFilterBy<T> filter, SortBy<T> sort, string businessEntityID, bool autoAssociateWithMultipleParents, EntityDefinition definition = null, List<string> parentIDs = null, Tuple<Dictionary<string, AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> propertiesInfo = null) where T : class
		{
			definition = definition ?? (autoAssociateWithMultipleParents ? RepositoryMediator.GetEntityDefinition<T>() : null);
			propertiesInfo = propertiesInfo ?? RepositoryMediator.GetProperties<T>(businessEntityID, definition);
			parentIDs = parentIDs ?? (definition != null && autoAssociateWithMultipleParents && filter != null ? filter.GetAssociatedParentIDs(definition) : null);

			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;

			var filterBy = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetNoSqlStatement(standardProperties, extendedProperties, definition, parentIDs)
					: (filter as FilterBy<T>).GetNoSqlStatement(standardProperties, extendedProperties, definition, parentIDs)
				: null;

			if (!string.IsNullOrWhiteSpace(businessEntityID) && extendedProperties != null)
				filterBy = filterBy == null
					? Builders<T>.Filter.Eq("EntityID", businessEntityID)
					: filterBy & Builders<T>.Filter.Eq("EntityID", businessEntityID);

			var sortBy = sort?.GetNoSqlStatement(null, standardProperties, extendedProperties);

			return new Tuple<FilterDefinition<T>, SortDefinition<T>>(filterBy, sortBy);
		}
		#endregion

	}

}