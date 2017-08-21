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
		public FilterBy(CompareOperator @operator = CompareOperator.Equals, string attribute = null, object value = null) : this(null, @operator, attribute, value) { }

		public FilterBy(JObject json, CompareOperator @operator = CompareOperator.Equals, string attribute = null, object value = null)
		{
			this.Operator = @operator;
			this.Attribute = attribute;
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
		public void Parse(JObject json)
		{
			if (json != null)
			{
				var @operator = json["Operator"];
				this.Operator = @operator != null && @operator is JValue && (@operator as JValue).Value != null
					? (@operator as JValue).Value.ToString().ToEnum<CompareOperator>()
					: CompareOperator.Equals;

				var attribute = json["Attribute"];
				this.Attribute = attribute != null && attribute is JValue && (attribute as JValue).Value != null
					? (attribute as JValue).Value.ToString()
					: null;

				var value = json["Value"];
				this.Value = value != null && value is JValue
					? (value as JValue).Value
					: null;
			}
		}

		public JObject ToJson()
		{
			return new JObject()
			{
				{ "Operator", this.Operator.ToString() },
				{ "Attribute", this.Attribute },
				{ "Value", new JValue(this.Value) }
			};
		}

		public override string ToString()
		{
#if DEBUG
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
#else
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
#endif
		}
		#endregion

		#region Working with SQL
		internal Tuple<string, Dictionary<string, object>> GetSqlStatement(string surfix, Dictionary<string, ObjectService.AttributeInfo> standardProperties = null, Dictionary<string, ExtendedPropertyDefinition> extendedProperties = null, EntityDefinition definition = null, List<string> parentIDs = null)
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
						+ "Origin." + this.Attribute + "=@" + this.Attribute + surf
						+ " OR Link." + definition.MultipleParentAssociatesMapColumn + "=@" + this.Attribute + surf + "_L";

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
				var value = this.Value;

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
						@operator = "LIKE '%@" + name + "%'";
						gotName = false;
						break;

					case CompareOperator.StartsWith:
						@operator = "LIKE '@" + name + "%'";
						gotName = false;
						break;

					case CompareOperator.EndsWith:
						@operator = "LIKE '%@" + name + "'";
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
					+ @operator + (gotName ? "@" + name : "");

				if (value != null)
					parameters.Add("@" + name, value);
			}

			return new Tuple<string, Dictionary<string, object>>(statement, parameters);
		}

		public Tuple<string, Dictionary<string, object>> GetSqlStatement()
		{
			return this.GetSqlStatement(null);
		}
		#endregion

		#region Working with No SQL
		internal FilterDefinition<T> GetNoSqlStatement(Dictionary<string, ObjectService.AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties, EntityDefinition definition = null, List<string> parentIDs = null)
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
				switch (this.Operator)
				{
					case CompareOperator.Equals:
						filter = Builders<T>.Filter.Eq(attribute, this.Value);
						break;

					case CompareOperator.NotEquals:
						filter = Builders<T>.Filter.Ne(attribute, this.Value);
						break;

					case CompareOperator.LessThan:
						filter = Builders<T>.Filter.Lt(attribute, this.Value);
						break;

					case CompareOperator.LessThanOrEquals:
						filter = Builders<T>.Filter.Lte(attribute, this.Value);
						break;

					case CompareOperator.Greater:
						filter = Builders<T>.Filter.Gt(attribute, this.Value);
						break;

					case CompareOperator.GreaterOrEquals:
						filter = Builders<T>.Filter.Gte(attribute, this.Value);
						break;

					case CompareOperator.Contains:
						filter = this.Value == null || !this.Value.GetType().IsStringType() || this.Value.Equals("")
							? Builders<T>.Filter.Eq(attribute, "")
							: Builders<T>.Filter.Regex(attribute, new BsonRegularExpression("/.*" + this.Value + ".*/"));
						break;

					case CompareOperator.StartsWith:
						filter = this.Value == null || !this.Value.GetType().IsStringType() || this.Value.Equals("")
							? Builders<T>.Filter.Eq(attribute, "")
							: Builders<T>.Filter.Regex(attribute, new BsonRegularExpression("^" + this.Value));
						break;

					case CompareOperator.EndsWith:
						filter = this.Value == null || !this.Value.GetType().IsStringType() || this.Value.Equals("")
							? Builders<T>.Filter.Eq(attribute, "")
							: Builders<T>.Filter.Regex(attribute, new BsonRegularExpression(this.Value + "$"));
						break;

					case CompareOperator.IsNull:
						filter = Builders<T>.Filter.Eq(attribute, BsonNull.Value);
						break;

					case CompareOperator.IsNotNull:
						filter = Builders<T>.Filter.Ne(attribute, BsonNull.Value);
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

			return filter;
		}

		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		public FilterDefinition<T> GetNoSqlStatement()
		{
			return this.GetNoSqlStatement(null, null);
		}
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// Filtering expression for using with combining operators (AND/OR)
	/// </summary>
	[Serializable]
	public class FilterBys<T> : IFilterBy<T> where T : class
	{
		public FilterBys(GroupOperator @operator = GroupOperator.And, List<IFilterBy<T>> children = null) : this(null, @operator, children) { }

		public FilterBys(JObject json, GroupOperator @operator = GroupOperator.And, List<IFilterBy<T>> children = null)
		{
			this.Operator = @operator;
			this.Children = children != null
				? children
				: new List<IFilterBy<T>>();
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
		/// Adds an expression into the collection of children
		/// </summary>
		/// <param name="filter"></param>
		public void Add(IFilterBy<T> filter)
		{
			if (filter != null)
				this.Children.Add(filter);
		}

		#region Working with JSON
		public void Parse(JObject json)
		{
			if (json != null)
			{
				var @operator = json["Operator"];
				this.Operator = @operator != null && @operator is JValue && (@operator as JValue).Value != null
					? (@operator as JValue).Value.ToString().ToEnum<GroupOperator>()
					: GroupOperator.And;

				var childrenJson = json["Children"];
				if (childrenJson != null && childrenJson is JArray)
					foreach (JObject childJson in childrenJson as JArray)
					{
						var childOperator = (childJson["Operator"] as JValue).Value.ToString();
						var childFilter = childOperator.IsEquals("And") || childOperator.IsEquals("Or")
							? new FilterBys<T>() as IFilterBy<T>
							: new FilterBy<T>() as IFilterBy<T>;
						childFilter.Parse(childJson);

						this.Children.Add(childFilter);
					}
			}
		}

		public JObject ToJson()
		{
			return new JObject()
			{
				{ "Operator", this.Operator.ToString() },
				{ "Children", this.Children.Select(c => c.ToJson()).ToList().ToJArray() }
			};
		}

		public override string ToString()
		{
#if DEBUG
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
#else
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
#endif
		}
		#endregion

		#region Working with statement of SQL
		internal Tuple<string, Dictionary<string, object>> GetSqlStatement(string surfix, Dictionary<string, ObjectService.AttributeInfo> standardProperties = null, Dictionary<string, ExtendedPropertyDefinition> extendedProperties = null, EntityDefinition definition = null, List<string> parentIDs = null)
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
						data.Item2.ForEach(parameter =>
						{
							parameters.Add(parameter.Key, parameter.Value);
						});
					}
				});

				return !statement.Equals("") && parameters.Count > 0
					? new Tuple<string, Dictionary<string, object>>((!string.IsNullOrEmpty(surfix) ? "(" : "") + statement + (!string.IsNullOrEmpty(surfix) ? ")" : ""), parameters)
					: null;
			}
		}

		public Tuple<string, Dictionary<string, object>> GetSqlStatement()
		{
			return this.GetSqlStatement(null);
		}
		#endregion

		#region Working with statement of No SQL
		internal FilterDefinition<T> GetNoSqlStatement(Dictionary<string, ObjectService.AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties, EntityDefinition definition = null, List<string> parentIDs = null)
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
		{
			return this.GetNoSqlStatement(null, null);
		}
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
			if (filters != null)
				filters.ForEach(item =>
				{
					filter.Add(item);
				});
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
			if (filters != null)
				filters.ForEach(item =>
				{
					filter.Add(item);
				});
			return filter;
		}

		/// <summary>
		/// Creates a group of filter-by expressions with OR operator
		/// </summary>
		/// <param name="filters"></param>
		/// <returns></returns>
		public static FilterBys<T> Or(IEnumerable<IFilterBy<T>> filters)
		{
			return new FilterBys<T>(GroupOperator.Or, filters?.ToList());
		}
		#endregion

		#region Compare
		/// <summary>
		/// Creates a filter-by expressions with EQUALS operator (==)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Equals(string attribute, object value)
		{
			return new FilterBy<T>(CompareOperator.Equals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with NOT-EQUALS operator (!=)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> NotEquals(string attribute, object value)
		{
			return new FilterBy<T>(CompareOperator.NotEquals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with LESS THAN operator (&lt;)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> LessThan(string attribute, object value)
		{
			return new FilterBy<T>(CompareOperator.LessThan, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with LESS THAN or EQUALS operator (&lt;=)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> LessThanOrEquals(string attribute, object value)
		{
			return new FilterBy<T>(CompareOperator.LessThanOrEquals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with GREATER operator (&gt;)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Greater(string attribute, object value)
		{
			return new FilterBy<T>(CompareOperator.Greater, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with GREATER or EQUALS operator (&gt;=)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> GreaterOrEquals(string attribute, object value)
		{
			return new FilterBy<T>(CompareOperator.GreaterOrEquals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with CONTAINS operator (means the value of attribute must contains the sub-string)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Contains(string attribute, object value)
		{
			return new FilterBy<T>(CompareOperator.Contains, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with STARTS WITH operator (means the value of attribute must starts with the sub-string)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> StartsWith(string attribute, string value)
		{
			return new FilterBy<T>(CompareOperator.StartsWith, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with ENDS WITH operator (means the value of attribute must ends with the sub-string)
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> EndsWith(string attribute, string value)
		{
			return new FilterBy<T>(CompareOperator.EndsWith, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS NULL operator (IS NULL)
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNull(string attribute)
		{
			return new FilterBy<T>(CompareOperator.IsNull, attribute, null);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS NOT NULL operator (IS NOT NULL)
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNotNull(string attribute)
		{
			return new FilterBy<T>(CompareOperator.IsNotNull, attribute, null);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS EMPTY operator (=='')
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsEmpty(string attribute)
		{
			return new FilterBy<T>(CompareOperator.IsEmpty, attribute, null);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS NOT EMPTY operator (!='')
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNotEmpty(string attribute)
		{
			return new FilterBy<T>(CompareOperator.IsNotEmpty, attribute, null);
		}
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// Sorting expression
	/// </summary>
	[Serializable]
	public class SortBy<T> where T : class
	{
		public SortBy(string attribute = null, SortMode mode = SortMode.Ascending) : this(null, attribute, mode) { }

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
				var attribute = json["Attribute"];
				this.Attribute = attribute != null && attribute is JValue && (attribute as JValue).Value != null
					? (attribute as JValue).Value.ToString()
					: null;

				var mode = json["Mode"];
				this.Mode = mode != null && mode is JValue && (mode as JValue).Value != null
					? (mode as JValue).Value.ToString().ToEnum<SortMode>()
					: SortMode.Ascending;

				var thenby = json["ThenBy"];
				this.ThenBy = thenby != null && thenby is JObject
					? new SortBy<T>(thenby as JObject)
					: null;
			}
		}

		/// <summary>
		/// Converts to JSON
		/// </summary>
		/// <returns></returns>
		public JObject ToJson()
		{
			return new JObject()
			{
				{ "Attribute", this.Attribute },
				{ "Mode", this.Mode.ToString() },
				{ "ThenBy", this.ThenBy?.ToJson() }
			};
		}

		public override string ToString()
		{
#if DEBUG
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
#else
			return this.ToJson().ToString(Newtonsoft.Json.Formatting.None);
#endif
		}
		#endregion

		#region Working with statement of SQL
		internal string GetSqlStatement(Dictionary<string, ObjectService.AttributeInfo> standardProperties, Dictionary<string, ExtendedPropertyDefinition> extendedProperties)
		{
			if (string.IsNullOrWhiteSpace(this.Attribute))
				return null;

			var next = this.ThenBy != null
				? this.ThenBy.GetSqlStatement(standardProperties, extendedProperties)
				: null;

			return this.Attribute + (this.Mode.Equals(SortMode.Ascending) ? " ASC" : " DESC") + (!string.IsNullOrWhiteSpace(next) ? ", " + next : "");
		}

		/// <summary>
		/// Gets the statement of SQL
		/// </summary>
		/// <returns></returns>
		public SortDefinition<T> GetSqlStatement()
		{
			return this.GetSqlStatement(null, null);
		}
		#endregion

		#region Working with statement of No SQL
		internal SortDefinition<T> GetNoSqlStatement(SortDefinition<T> previous, Dictionary<string, ObjectService.AttributeInfo> standardProperties = null, Dictionary<string, ExtendedPropertyDefinition> extendedProperties = null)
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
		{
			return this.GetNoSqlStatement(null);
		}
		#endregion

		internal List<string> GetAttributes()
		{
			var attributes = new List<string>() { this.Attribute };
			return this.ThenBy != null
				? attributes.Concat(this.ThenBy.GetAttributes()).ToList()
				: attributes;
		}

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
		{
			return new SortBy<T>(attribute, SortMode.Ascending);
		}

		/// <summary>
		/// Creates a descending sort
		/// </summary>
		public static SortBy<T> Descending(string attribute)
		{
			return new SortBy<T>(attribute, SortMode.Descending);
		}
	}

	// ------------------------------------------

	/// <summary>
	/// Extension methods for working with repository restrictions
	/// </summary>
	public static class Extensions
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
		internal static Tuple<Tuple<string, Dictionary<string, object>>, string> PrepareSqlStatements<T>(IFilterBy<T> filter, SortBy<T> sort, string businessEntityID, bool autoAssociateWithMultipleParents, EntityDefinition definition = null, List<string> parentIDs = null, Tuple<Dictionary<string, ObjectService.AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> propertiesInfo = null) where T : class
		{
			definition = definition != null
				? definition
				: RepositoryMediator.GetEntityDefinition<T>();

			propertiesInfo = propertiesInfo != null
				? propertiesInfo
				: RepositoryMediator.GetProperties<T>(businessEntityID, definition);

			parentIDs = parentIDs != null
				? parentIDs
				: definition != null && autoAssociateWithMultipleParents && filter != null
					? filter.GetAssociatedParentIDs(definition)
					: null;

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

			var sortBy = sort != null
				? sort.GetSqlStatement(standardProperties, extendedProperties)
				: null;

			return new Tuple<Tuple<string, Dictionary<string, object>>, string>(filterBy, sortBy);
		}
		#endregion

		#region Statements of No SQL
		internal static Tuple<FilterDefinition<T>, SortDefinition<T>> PrepareNoSqlStatements<T>(IFilterBy<T> filter, SortBy<T> sort, string businessEntityID, bool autoAssociateWithMultipleParents, EntityDefinition definition = null, List<string> parentIDs = null, Tuple<Dictionary<string, ObjectService.AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> propertiesInfo = null) where T : class
		{
			definition = definition != null
				? definition
				: autoAssociateWithMultipleParents
					? RepositoryMediator.GetEntityDefinition<T>()
					: null;

			propertiesInfo = propertiesInfo != null
				? propertiesInfo
				: RepositoryMediator.GetProperties<T>(businessEntityID, definition);

			parentIDs = parentIDs != null
				? parentIDs
				: definition != null && autoAssociateWithMultipleParents && filter != null
					? filter.GetAssociatedParentIDs(definition)
					: null;

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

			var sortBy = sort != null
				? sort.GetNoSqlStatement(null, standardProperties, extendedProperties)
				: null;

			return new Tuple<FilterDefinition<T>, SortDefinition<T>>(filterBy, sortBy);
		}
		#endregion

	}

}