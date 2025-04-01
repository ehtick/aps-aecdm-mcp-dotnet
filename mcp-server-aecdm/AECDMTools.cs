using ModelContextProtocol.Server;
using System.ComponentModel;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using ModelContextProtocol.Protocol.Messages;
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

	[McpServerTool, Description("Get the ACC folders from one project")]
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
		JToken elementGroups = jsonData.SelectToken("elementGroupsByProject.results");

		return elementGroups.ToString();
	}
}