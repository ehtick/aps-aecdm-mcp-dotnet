using ModelContextProtocol.Server;
using System.ComponentModel;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;


using Autodesk.Data;
using Autodesk.Data.DataModels;
using Autodesk.GeometryUtilities.ConversionAPI;
using Autodesk.SDKManager;
using System.IO;
using Autodesk.DataManagement.Model;
using Autodesk.Data.Interface;
using Autodesk.Data.OpenAPI;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class AECDMTools
{
    private const string BASE_URL = "https://developer-stg.api.autodesk.com/aec/graphql";


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
							alternativeIdentifiers{
							  dataManagementAPIHubId
							}

                        }
                    }
                }",
		};

		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JArray hubs = (JArray)jsonData.SelectToken("hubs.results");
        List<Hub> hubList = new List<Hub>();
        foreach (var hub in hubs.ToList())
        {
            try
            {
                Hub newHub = new Hub();
                newHub.id = hub.SelectToken("id").ToString();
                newHub.name = hub.SelectToken("name").ToString();
                newHub.dataManagementAPIHubId = hub.SelectToken("alternativeIdentifiers.dataManagementAPIHubId").ToString();
                hubList.Add(newHub);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        string hubsString = hubList.Select(hub => hub.ToString()).Aggregate((a, b) => $"{a}, {b}");
        return hubsString;
    }

    [McpServerTool, Description("Get the ACC projects from one hub")]
	public static async Task<string> GetProjects([Description("Hub id, don't use dataManagementAPIHubId")]string hubId)
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
							alternativeIdentifiers{
							  dataManagementAPIProjectId
							}
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
        JArray projects = (JArray)jsonData.SelectToken("projects.results");

        List<Project> projectList = new List<Project>();
        foreach (var project in projects.ToList())
        {
            try
            {
                var newProject = new Project();
                newProject.id = project.SelectToken("id").ToString();
                newProject.name = project.SelectToken("name").ToString();
                newProject.dataManagementAPIProjectId = project.SelectToken("alternativeIdentifiers.dataManagementAPIProjectId").ToString();
                projectList.Add(newProject);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        string projectsString = projectList.Select(project => project.ToString()).Aggregate((a, b) => $"{a}, {b}");
        return projectsString;
    }

	[McpServerTool, Description("Get the Designs/Models/ElementGroups from one project")]
	public static async Task<string> GetElementGroupsByProject([Description("Project id, don't use dataManagementAPIProjectId")]string projectId)
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

	[McpServerTool, Description("Get the Elements from the ElementGroup/Design using a category filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment")]
	public static async Task<string> GetElementsByElementGroupWithCategoryFilter([Description("ElementGroup id, not the file version urn")] string elementGroupId, [Description("Category name to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar")] string category)
	{
		var query = new GraphQLRequest
		{
			Query = @"
			query GetElementsByElementGroupWithFilter ($elementGroupId: ID!, $filter: String!) {
			  elementsByElementGroup(elementGroupId: $elementGroupId, pagination: {limit:500}, filter: {query:$filter}) {
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
                filter = $"'property.name.category'=='{category}' and 'property.name.Element Context'=='Instance'"
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

		string elementsString = elementsList.Select(el => el.ToString()).Aggregate((a,b)=>$"{a}; {b}");
        Console.WriteLine(elementsString);
        return elementsString;
	}


    [McpServerTool, Description("Get the Elements from the ElementGroup/Design using a filter. The filter is a string that will be used in the GraphQL query. For example: 'property.name.category'=='Walls' and 'property.name.Element Context'=='Instance'")]
    public static async Task<string> ExportIfcForElementGroup(
        [Description("ElementGroup id, not the file version urn")] string elementGroupId,
        [Description("Category name to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar")] string category,
        [Description("File name of this exported IFC file.")] string fileName = null)
    {
        string path = string.Empty;
        try
        {
            var elementGroup = Autodesk.Data.DataModels.ElementGroup.Create(Global.SDKClient);
            var aecdmService = new AECDMService(Global.SDKClient);

            List<AECDMElement> elements = await aecdmService.GetAllElementsByElementGroupParallelAsync(elementGroupId);
            var newElements = elements.ToList().Where(el =>
            {
                var propCategory = el.Properties.Results.Where(prop => prop.Name == "Revit Category Type Id").ToList();
                var propContext = el.Properties.Results.Where(prop => prop.Name == "Element Context").ToList();
                return (propCategory.Count > 0 && propContext.Count > 0 && propContext[0].Value == "Instance" && propCategory[0].Value == category);
            }).ToList();

            elementGroup.AddAECDMElements(newElements);
            path = await elementGroup.ConvertToIfc(ifcFileId: fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nApplication failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            path = ex.Message;
        }

        return path;
    }


    [McpServerTool, Description("Export Ifc file for the selected element")]
    public static async Task<string> ExportIfcForElements(
		[Description("Array of element ids that will be exported to IFC!")] string[] elementIds, 
		[Description("File name of this exported IFC file.")] string fileName = null)
    {
		string path = string.Empty;
		try
        {
            var elementGroup = Autodesk.Data.DataModels.ElementGroup.Create(Global.SDKClient);
            var aecdmService = new AECDMService(Global.SDKClient);

            List<AECDMElement> elements = new List<AECDMElement>();
			var tasks = elementIds.ToList().Select(async (item) =>
			{
				var element = await aecdmService.GetElementData(item);
				elements.Add(element.Data.ElementAtTip);
			});
			await Task.WhenAll(tasks);

			// Alternatively, add elements in a batch
			elementGroup.AddAECDMElements(elements);
            path = await elementGroup.ConvertToIfc( ifcFileId:fileName );
        }
        catch (Exception ex)
        {
			Console.WriteLine($"\nApplication failed with error: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
            path = ex.Message;
        }
        return path;
    }




    [McpServerTool, Description("Export Mesh data for the selected elements")]
    public static async Task<string> ExportMeshForElements(
        [Description("Array of element ids!")] string[] elementIds)
    {
        try
        {
            var elementGroup = Autodesk.Data.DataModels.ElementGroup.Create(Global.SDKClient);
            var aecdmService = new AECDMService(Global.SDKClient);

            List<AECDMElement> elements = new List<AECDMElement>();
            var tasks = elementIds.ToList().Select(async (item) =>
            {
                var element = await aecdmService.GetElementData(item);
                elements.Add(element.Data.ElementAtTip);
            });
            await Task.WhenAll(tasks);

            // Alternatively, add elements in a batch
            elementGroup.AddAECDMElements(elements);
            //Get ElementGeometriesAsMesh
            var ElementGeomMap = await elementGroup.GetElementGeometriesAsMeshAsync().ConfigureAwait(false);

            foreach (KeyValuePair<Autodesk.Data.DataModels.Element, IEnumerable<ElementGeometry>> kv in ElementGeomMap)
            {
                var element = kv.Key;
                var meshObjList = kv.Value;
                Console.WriteLine($"Element ID: {element.Id} has {meshObjList.Count()} meshes");
                foreach (var meshObj in meshObjList)
                {
                    if (meshObj is Autodesk.Data.DataModels.MeshGeometry meshGeometry)
                    {
                        // Do something with the mesh object, e.g., print its properties
                        Console.WriteLine($"Mesh Vertices Count: {meshGeometry.Mesh?.Vertices.Count}");
                    }
                    else
                    {
                        Console.WriteLine($"Element ID: {element.Id} has no geometry.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nApplication failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        return "Mesh are generated";
    }
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


internal class Hub
{

   internal string id { get; set; }
    internal string name { get; set; }
    internal string dataManagementAPIHubId { get; set; }

    public override string ToString()
    {
        return $"hubId: {id}, hubName: {name}, dataManagementAPIHubId: {dataManagementAPIHubId}";
    }
}

internal class Project
{
    internal string id { get; set; }
    internal string name { get; set; }
    internal string dataManagementAPIProjectId { get; set; }

    public override string ToString()
    {
        return $"projectId: {id}, projectName: {name}, dataManagementAPIProjectId: {dataManagementAPIProjectId}";
    }
}	




internal class Element
{
	internal string id { get; set; }
	internal string name { get; set; }
	internal List<Property> properties { get; set; }

	public override string ToString()
	{
		var externalIdProp = properties.Find(prop => prop.name == "External ID");
		return $"id: {id}, name: {name}, external id: {externalIdProp.value} ";
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