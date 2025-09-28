using ModelContextProtocol.Server;
using System.ComponentModel;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class AECDMTools
{
	private const string BASE_URL = "https://developer.api.autodesk.com/aec/graphql";

	public static async Task<object> Query(GraphQL.GraphQLRequest query, string regionHeader = null)
	{
		var client = new GraphQLHttpClient(BASE_URL, new NewtonsoftJsonSerializer());
		client.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Global.AccessToken);
		if (!String.IsNullOrWhiteSpace(regionHeader))
			client.HttpClient.DefaultRequestHeaders.Add("region", regionHeader);
		var response = await client.SendQueryAsync<object>(query);

		if (response.Data == null) return response.Errors[0].Message;
		return response.Data;
	}

	[McpServerTool, Description("Get the ACC hubs from the user")]
	public static async Task<string> GetHubs()
	{
		var query = new GraphQL.GraphQLRequest
		{
			Query = @"
                query {
                    hubs {
                        pagination {
                            cursor
                        }
                        results {
                            id
                            name
                        }
                    }
                }",
		};

		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JToken hubsResults = jsonData.SelectToken("hubs.results");

		return hubsResults.ToString();
	}

	[McpServerTool, Description("Get the ACC projects from one hub")]
	public static async Task<string> GetProjects([Description("Hub id to query the projects from")]string hubId)
	{
		var query = new GraphQLRequest
		{
			Query = @"
			    query GetProjects ($hubId: ID!) {
			        projects (hubId: $hubId) {
                        pagination {
                           cursor
                        }
                        results {
                            id
                            name
                        }
			        }
			    }",
			Variables = new
			{
				hubId = hubId
			}
		};

		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JToken projectsResults = jsonData.SelectToken("projects.results");

		return projectsResults.ToString();
	}

	[McpServerTool, Description("Get the ACC ElementGroups from one project")]
	public static async Task<string> GetElementGroupsByProject([Description("Project id used to query the elemengroups from")]string projectId)
	{
		var query = new GraphQLRequest
		{
			Query = @"
			    query GetElementGroupsByProject ($projectId: ID!) {
			        elementGroupsByProject(projectId: $projectId) {
			            results{
			                id
			                name
											alternativeIdentifiers{
												fileVersionUrn
											}
			            }
			        }
			    }",
			Variables = new
			{
				projectId = projectId
			}
		};
		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JArray elementGroups = (JArray)jsonData.SelectToken("elementGroupsByProject.results");

		List<ElementGroup> elementGroupsList = new List<ElementGroup>();
		//Loop through elementGroups
		foreach (var elementGroup in elementGroups.ToList())
		{
			try
			{
				ElementGroup newElementGroup = new ElementGroup();
				newElementGroup.id = elementGroup.SelectToken("id").ToString();
				newElementGroup.name = elementGroup.SelectToken("name").ToString();
				newElementGroup.fileVersionUrn = elementGroup.SelectToken("alternativeIdentifiers.fileVersionUrn").ToString();
				elementGroupsList.Add(newElementGroup);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
		string elementGroupsString = elementGroupsList.Select(eg => $"name: {eg.name}, id: {eg.id}, fileVersionUrn: {eg.fileVersionUrn}").Aggregate((a, b) => $"{a}, {b}");
		return elementGroupsString;
	}

	[McpServerTool, Description("Get the Elements from the ElementGroup using a category filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment")]
	public static async Task<string> GetElementsByElementGroupWithCategoryFilter([Description("ElementGroup id used to query the elements from")] string elementGroupId, [Description("Category name to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment")] string category)
	{
		var query = new GraphQLRequest
		{
			Query = @"
			query GetElementsByElementGroupWithFilter ($elementGroupId: ID!, $filter: String!) {
			  elementsByElementGroup(elementGroupId: $elementGroupId, filter: {query:$filter}) {
			    results{
			      id
			      name
			      properties {
			        results {
			            name
			            value
			        }
			      }
			    }
			  }
			}",
			Variables = new
			{
				elementGroupId = elementGroupId,
				filter = $"property.name.category=={category}"
			}
		};
		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JArray elements = (JArray)jsonData.SelectToken("elementsByElementGroup.results");

		List<Element> elementsList = new List<Element>();
		//Loop through elements 
		foreach (var element in elements.ToList())
		{
			try
			{
				Element newElement = new Element();
				newElement.id = element.SelectToken("id").ToString();
				newElement.name = element.SelectToken("name").ToString();
				newElement.properties = new List<Property>();
				JArray properties = (JArray)element.SelectToken("properties.results");
				foreach (JToken property in properties.ToList())
				{
					try
					{
						newElement.properties.Add(new Property
						{
							name = property.SelectToken("name").ToString(),
							value = property.SelectToken("value").ToString()
						});
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex.Message);
					}
				}
				elementsList.Add(newElement);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		string elementsString = elementsList.Select(el => $"name: {el.name}, id: {el.id}, {el.properties.Select(p => $"{p.name}:{p.value}").Aggregate((a, b) => $"{a}, {b}")}").Aggregate((a, b) => $"{a}, {b}");
		return elementsString;
	}

	//[McpServerTool, Description("Get the Elements from the ElementGroup using a properties and filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment")]
	//public static async Task<string> GetElementsByElementGroupWithCategoriesFilter([Description("ElementGroup id used to query the elements from")] string elementGroupId, [Description("Category name to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment")] string category)
	//{
	//	var query = new GraphQLRequest
	//	{
	//		Query = @"
	//		query GetElementsByElementGroupWithFilter ($elementGroupId: ID!, $filter: String!, $propertiesNames: [String!]!) {
	//		  elementsByElementGroup(elementGroupId: $elementGroupId, filter: {query:$filter}) {
	//		    results{
	//		      id
	//		      name
	//		      properties (filter:{names:$propertiesNames}){
	//		        results {
	//		            name
	//		            value
	//		        }
	//		      }
	//		    }
	//		  }
	//		}",
	//		Variables = new
	//		{
	//			elementGroupId = elementGroupId,
	//			filter = $"property.name.category=='{category}'",
	//			propertiesNames = new string[] { "External ID", "Area" }
	//		}
	//	};
	//	object data = await Query(query);

	//	JObject jsonData = JObject.FromObject(data);
	//	JArray elements = (JArray)jsonData.SelectToken("elementsByElementGroup.results");

	//	List<Element> elementsList = new List<Element>();
	//	//Loop through elements 
	//	foreach (var element in elements.ToList())
	//	{
	//		try
	//		{
	//			Element newElement = new Element();
	//			newElement.id = element.SelectToken("id").ToString();
	//			newElement.name = element.SelectToken("name").ToString();
	//			newElement.properties = new List<Property>();
	//			JArray properties = (JArray)element.SelectToken("properties.results");
	//			foreach (JToken property in properties.ToList())
	//			{
	//				try
	//				{
	//					newElement.properties.Add(new Property
	//					{
	//						name = property.SelectToken("name").ToString(),
	//						value = property.SelectToken("value").ToString()
	//					});
	//				}
	//				catch (Exception ex)
	//				{
	//					Console.WriteLine(ex.Message);
	//				}
	//			}
	//			elementsList.Add(newElement);
	//		}
	//		catch (Exception ex)
	//		{
	//			Console.WriteLine(ex.Message);
	//		}
	//	}

	//	string elementsString = elementsList.Select(el => $"name: {el.name}, id: {el.id}, {el.properties.Select(p => $"{p.name}:{p.value}").Aggregate((a, b) => $"{a}, {b}")}").Aggregate((a, b) => $"{a}, {b}");
	//	return elementsString;
	//}
}

internal class ElementGroup
{
	internal string id { get; set; }
	internal string name { get; set; }
	internal string fileVersionUrn { get; set; }

	public override string ToString()
	{
		return $"id: {id}, name: {name}, fileVersionUrn: {fileVersionUrn}";
	}
}

internal class Element
{
	internal string id { get; set; }
	internal string name { get; set; }
	internal List<Property> properties { get; set; }

	public override string ToString()
	{
		return $"id: {id}, name: {name}, properties: {
			properties.Select(p => $"name: {p.name}, value: {p.value}").Aggregate((a, b) => $"{a}, {b}")}";
	}
}

internal class Property
{
	internal string name { get; set; }
	internal string value { get; set; }
	public override string ToString()
	{
		return $"name: {name}, value: {value}";
	}
}
