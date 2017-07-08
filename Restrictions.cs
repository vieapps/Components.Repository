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

using MongoDB.Driver;
using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// An expression for filtering using comparing operators
	/// </summary>
	[Serializable]
	public class FilterBy<T> : IFilterBy<T> where T : class
	{

		#region Constructors
		public FilterBy(CompareOperator @operator = CompareOperator.Equals, string attribute = null, object value = null)
		{
			this.Operator = @operator;
			this.Attribute = attribute;
			this.Value = value;
		}

		public FilterBy(JObject json)
		{
			this.Parse(json);
		}
		#endregion

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
		[JsonIgnore, XmlIgnore]
		public FilterBys<T> Parent { get; internal set; }

		#region Working with JSON
		public void Parse(JObject json)
		{

		}

		public JObject ToJson()
		{
			var json = new JObject()
			{
				{ "Operator", this.Operator.ToString() },
				{ "Attribute", this.Attribute },
			};
			json.Add(new JProperty("Value", this.Value));
			return json;
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
		internal Tuple<string, Dictionary<string, object>> GetSqlStatement(string surfix)
		{
			return string.IsNullOrEmpty(this.Attribute)
				? null
				: new Tuple<string, Dictionary<string, object>>(
						this.Attribute + "=@" + this.Attribute + (!string.IsNullOrEmpty(surfix) ? "_" + surfix : ""),
						new Dictionary<string, object>() {
							{ "@" + this.Attribute + (!string.IsNullOrEmpty(surfix) ? "_" + surfix : ""), this.Value }
						}
					);
		}

		public Tuple<string, Dictionary<string, object>> GetSqlStatement()
		{
			return this.GetSqlStatement(null);
		}
		#endregion

		#region Working with No SQL
		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		public FilterDefinition<T> GetNoSqlStatement()
		{
			if (string.IsNullOrWhiteSpace(this.Attribute))
				return null;

			switch (this.Operator)
			{
				case CompareOperator.Equals:
					return Builders<T>.Filter.Eq(this.Attribute, this.Value);

				case CompareOperator.NotEquals:
					return Builders<T>.Filter.Ne(this.Attribute, this.Value);

				case CompareOperator.LessThan:
					return Builders<T>.Filter.Lt(this.Attribute, this.Value);

				case CompareOperator.LessThanOrEquals:
					return Builders<T>.Filter.Lte(this.Attribute, this.Value);

				case CompareOperator.Greater:
					return Builders<T>.Filter.Gt(this.Attribute, this.Value);

				case CompareOperator.GreaterOrEquals:
					return Builders<T>.Filter.Gte(this.Attribute, this.Value);

				case CompareOperator.Contains:
					return this.Value == null || !this.Value.GetType().IsStringType() || this.Value.Equals("")
						? Builders<T>.Filter.Eq(this.Attribute, "")
						: Builders<T>.Filter.Regex(this.Attribute, new BsonRegularExpression("/.*" + this.Value + ".*/"));

				case CompareOperator.StartsWith:
					return this.Value == null || !this.Value.GetType().IsStringType() || this.Value.Equals("")
						? Builders<T>.Filter.Eq(this.Attribute, "")
						: Builders<T>.Filter.Regex(this.Attribute, new BsonRegularExpression("^" + this.Value));

				case CompareOperator.EndsWith:
					return this.Value == null || !this.Value.GetType().IsStringType() || this.Value.Equals("")
						? Builders<T>.Filter.Eq(this.Attribute, "")
						: Builders<T>.Filter.Regex(this.Attribute, new BsonRegularExpression(this.Value + "$"));

				case CompareOperator.IsNull:
					return Builders<T>.Filter.Eq(this.Attribute, BsonNull.Value);

				case CompareOperator.IsNotNull:
					return Builders<T>.Filter.Ne(this.Attribute, BsonNull.Value);

				case CompareOperator.IsEmpty:
					return Builders<T>.Filter.Eq(this.Attribute, "");

				case CompareOperator.IsNotEmpty:
					return Builders<T>.Filter.Ne(this.Attribute, "");

				default:
					return null;
			}
		}
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// An expression for filtering using combining operators (AND/OR)
	/// </summary>
	[Serializable]
	public class FilterBys<T> : IFilterBy<T> where T : class
	{

		#region Constructors
		public FilterBys(GroupOperator @operator = GroupOperator.And, List<IFilterBy<T>> children = null)
		{
			this.Operator = @operator;
			this.Children = children != null ? children : new List<IFilterBy<T>>();
		}

		public FilterBys(JObject json)
		{
			this.Parse(json);
		}
		#endregion

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

		/// <summary>
		/// Adds a child into collection of children
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

		}

		public JObject ToJson()
		{
			var children = new JArray();
			this.Children.ForEach(child =>
			{
				children.Add(child.ToJson());
			});
			return new JObject()
			{
				{ "Operator", this.Operator.ToString() },
				{ "Children", children }
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
		internal Tuple<string, Dictionary<string, object>> GetSqlStatement(string surfix)
		{
			if (this.Children == null || this.Children.Count < 1)
				return null;

			else if (this.Children.Count.Equals(1))
				return this.Children[0].GetSqlStatement();

			else
			{
				var statement = "";
				var parameters = new Dictionary<string, object>();
				this.Children.ForEach((child, index) =>
				{
					var data = (child is FilterBys<T>)
						? (child as FilterBys<T>).GetSqlStatement((!string.IsNullOrEmpty(surfix) ? surfix : "r") + "_" + index.ToString())
						: (child as FilterBy<T>).GetSqlStatement((!string.IsNullOrEmpty(surfix) ? surfix : "r") + "_" + index.ToString());

					if (data != null)
					{
						statement += (statement.Equals("") ? "" : this.Operator.Equals(GroupOperator.And) ? " AND " : " OR ") + data.Item1;
						data.Item2.ForEach(p =>
						{
							parameters.Add(p.Key, p.Value);
						});
					}
				});

				return !statement.Equals("") && parameters.Count > 0
					? new Tuple<string, Dictionary<string, object>>("(" + statement + ")", parameters)
					: null;
			}
		}

		public Tuple<string, Dictionary<string, object>> GetSqlStatement()
		{
			return this.GetSqlStatement(null);
		}
		#endregion

		#region Working with statement of No SQL
		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		public FilterDefinition<T> GetNoSqlStatement()
		{
			FilterDefinition<T> filter = null;

			if (this.Children == null || this.Children.Count < 1)
				filter = Builders<T>.Filter.Empty;

			else if (this.Children.Count.Equals(1))
				filter = this.Children[0].GetNoSqlStatement();

			else
				this.Children.ForEach(child =>
				{
					var childFilter = child.GetNoSqlStatement();
					if (childFilter != null)
						filter = filter == null
							? childFilter
							: this.Operator.Equals(GroupOperator.And) ? filter & childFilter : filter | childFilter;
				});

			return filter;
		}
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// Wrappers of all available expressions for filtering
	/// </summary>
	public static class Filters
	{

		#region Group
		/// <summary>
		/// Creates a group of filter-by expressions with AND operator
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static FilterBys<T> And<T>() where T : class
		{
			return Filters.And<T>(new List<IFilterBy<T>>());
		}

		/// <summary>
		/// Creates a group of filter-by expressions with AND operator
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filter"></param>
		/// <returns></returns>
		public static FilterBys<T> And<T>(IFilterBy<T> filter) where T : class
		{
			return new FilterBys<T>(GroupOperator.And, filter != null ? new List<IFilterBy<T>>() { filter } : null);
		}

		/// <summary>
		/// Creates a group of filter-by expressions with AND operator
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="children"></param>
		/// <returns></returns>
		public static FilterBys<T> And<T>(List<IFilterBy<T>> children) where T : class
		{
			return new FilterBys<T>(GroupOperator.And, children);
		}

		/// <summary>
		/// Creates a group of filter-by expressions with OR operator
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static FilterBys<T> Or<T>() where T : class
		{
			return Filters.Or<T>(new List<IFilterBy<T>>());
		}

		/// <summary>
		/// Creates a group of filter-by expressions with OR operator
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filter"></param>
		/// <returns></returns>
		public static FilterBys<T> Or<T>(IFilterBy<T> filter) where T : class
		{
			return new FilterBys<T>(GroupOperator.Or, filter != null ? new List<IFilterBy<T>>() { filter } : null);
		}

		/// <summary>
		/// Creates a group of filter-by expressions with OR operator
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="children"></param>
		/// <returns></returns>
		public static FilterBys<T> Or<T>(List<IFilterBy<T>> children) where T : class
		{
			return new FilterBys<T>(GroupOperator.Or, children);
		}
		#endregion

		#region Compare
		/// <summary>
		/// Creates a filter-by expressions with EQUALS operator (==)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Equals<T>(string attribute, object value) where T : class
		{
			return new FilterBy<T>(CompareOperator.Equals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with NOT-EQUALS operator (!=)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> NotEquals<T>(string attribute, object value) where T : class
		{
			return new FilterBy<T>(CompareOperator.NotEquals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with LESS THAN operator (&lt;)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> LessThan<T>(string attribute, object value) where T : class
		{
			return new FilterBy<T>(CompareOperator.LessThan, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with LESS THAN or EQUALS operator (&lt;=)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> LessThanOrEquals<T>(string attribute, object value) where T : class
		{
			return new FilterBy<T>(CompareOperator.LessThanOrEquals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with GREATER operator (&gt;)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Greater<T>(string attribute, object value) where T : class
		{
			return new FilterBy<T>(CompareOperator.Greater, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with GREATER or EQUALS operator (&gt;=)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> GreaterOrEquals<T>(string attribute, object value) where T : class
		{
			return new FilterBy<T>(CompareOperator.GreaterOrEquals, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with CONTAINS operator (means the value of attribute must contains the sub-string)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> Contains<T>(string attribute, object value) where T : class
		{
			return new FilterBy<T>(CompareOperator.Contains, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with STARTS WITH operator (means the value of attribute must starts with the sub-string)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> StartsWith<T>(string attribute, string value) where T : class
		{
			return new FilterBy<T>(CompareOperator.StartsWith, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with ENDS WITH operator (means the value of attribute must ends with the sub-string)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static FilterBy<T> EndsWith<T>(string attribute, string value) where T : class
		{
			return new FilterBy<T>(CompareOperator.EndsWith, attribute, value);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS NULL operator (IS NULL)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNull<T>(string attribute) where T : class
		{
			return new FilterBy<T>(CompareOperator.IsNull, attribute, null);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS NOT NULL operator (IS NOT NULL)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNotNull<T>(string attribute) where T : class
		{
			return new FilterBy<T>(CompareOperator.IsNotNull, attribute, null);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS EMPTY operator (=='')
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsEmpty<T>(string attribute) where T : class
		{
			return new FilterBy<T>(CompareOperator.IsEmpty, attribute, null);
		}

		/// <summary>
		/// Creates a filter-by expressions with IS NOT EMPTY operator (!='')
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static FilterBy<T> IsNotEmpty<T>(string attribute) where T : class
		{
			return new FilterBy<T>(CompareOperator.IsNotEmpty, attribute, null);
		}
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// An expression for sorting
	/// </summary>
	[Serializable]
	public class SortBy<T> where T : class
	{

		#region Constructors
		public SortBy(string attribute = null, SortMode mode = SortMode.Ascending)
		{
			this.Attribute = attribute;
			this.Mode = mode;
		}

		public SortBy(JObject json)
		{
			this.Parse(json);
		}
		#endregion

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

		#region Working with JSON
		/// <summary>
		/// Parses from JSON
		/// </summary>
		/// <param name="json"></param>
		public void Parse(JObject json)
		{

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
				{ "Operator", this.Mode.ToString() },
				{ "ThenBy", this.ThenBy != null ? this.ThenBy.ToJson() : null }
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
		/// <summary>
		/// Gets the statement of SQL
		/// </summary>
		/// <returns></returns>
		public string GetSqlStatement()
		{
			if (string.IsNullOrWhiteSpace(this.Attribute))
				return null;

			var next = this.ThenBy != null
				? this.ThenBy.GetSqlStatement()
				: null;

			return this.Attribute + (this.Mode.Equals(SortMode.Ascending) ? " ASC" : " DESC") + (!string.IsNullOrWhiteSpace(next) ? ", " + next : "");
		}
		#endregion

		#region Working with statement of No SQL
		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		public SortDefinition<T> GetNoSqlStatement()
		{
			return this.GetNoSqlStatement(null);
		}

		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <param name="previous"></param>
		/// <returns></returns>
		public SortDefinition<T> GetNoSqlStatement(SortDefinition<T> previous)
		{
			if (string.IsNullOrWhiteSpace(this.Attribute))
				return null;

			var sort = previous != null
				? this.Mode.Equals(SortMode.Ascending)
					? previous.Ascending(this.Attribute)
					: previous.Descending(this.Attribute)
				: this.Mode.Equals(SortMode.Ascending)
					? Builders<T>.Sort.Ascending(this.Attribute)
					: Builders<T>.Sort.Descending(this.Attribute);

			return this.ThenBy != null
				? this.ThenBy.GetNoSqlStatement(sort)
				: sort;
		}
		#endregion

	}

	// ------------------------------------------

	/// <summary>
	/// Wrappers of all available expressions for sorting
	/// </summary>
	public static class Sorts
	{

		#region Ascending
		/// <summary>
		/// Creates an ascending sort
		/// </summary>
		public static SortBy<T> Ascending<T>(string attribute) where T : class
		{
			return new SortBy<T>(attribute, SortMode.Ascending);
		}

		/// <summary>
		/// Creates a combined sorting expression with an ascending sort
		/// </summary>
		public static SortBy<T> ThenByAscending<T>(this SortBy<T> sort, string attribute) where T : class
		{
			sort.ThenBy = new SortBy<T>(attribute, SortMode.Ascending);
			return sort;
		}
		#endregion

		#region Descending
		/// <summary>
		/// Creates a descending sort
		/// </summary>
		public static SortBy<T> Descending<T>(string attribute) where T : class
		{
			return new SortBy<T>(attribute, SortMode.Descending);
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

	}

}