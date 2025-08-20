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
using System.Text;
using System.Numerics;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class AECDMTools
{
    private const string BASE_URL = "https://developer-stg.api.autodesk.com/aec/graphql";


	public static async Task<object> Query(GraphQL.GraphQLRequest query, string? regionHeader = null)
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
        [Description("File name of this exported IFC file.")] string? fileName = null)
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
		[Description("File name of this exported IFC file.")] string? fileName = null)
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




    [McpServerTool, Description("Perform accurate clash detection analysis between selected building elements")]
    public static async Task<string> ClashDetectForElements(
        [Description("Array of element ids to analyze for clashes")] string[] elementIds,
        [Description("Minimum clash volume threshold in cubic units (default: 0.01 - higher values reduce false positives)")] double clashThreshold = 0.01)
    {
        var clashResults = new StringBuilder();
        var meshSummary = new StringBuilder();
        
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

            // Add elements to element group
            elementGroup.AddAECDMElements(elements);
            
            // Get ElementGeometriesAsMesh
            var ElementGeomMap = await elementGroup.GetElementGeometriesAsMeshAsync().ConfigureAwait(false);

            // Create element bounding boxes for clash detection analysis
            var elementCollisionData = new List<ElementBoundingBox>();
            
            // Process each element and create bounding boxes
            foreach (KeyValuePair<Autodesk.Data.DataModels.Element, IEnumerable<ElementGeometry>> kv in ElementGeomMap)
            {
                var element = kv.Key;
                var meshObjList = kv.Value;
                
                meshSummary.AppendLine($"Element ID: {element.Id} has {meshObjList.Count()} meshes");
                
                foreach (var meshObj in meshObjList)
                {
                    if (meshObj is Autodesk.Data.DataModels.MeshGeometry meshGeometry && meshGeometry.Mesh != null)
                    {
                        meshSummary.AppendLine($"  - Mesh Vertices Count: {meshGeometry.Mesh.Vertices.Count}");
                        
                        try
                        {
                            // Create bounding box from mesh vertices
                            var boundingBox = CreateBoundingBoxFromMesh(meshGeometry.Mesh);
                            if (boundingBox != null)
                            {
                                boundingBox.ElementId = element.Id;
                                boundingBox.ElementName = GetElementName(element);
                                elementCollisionData.Add(boundingBox);
                                
                                meshSummary.AppendLine($"  - Bounding Box: ({boundingBox.MinX:F2}, {boundingBox.MinY:F2}, {boundingBox.MinZ:F2}) to ({boundingBox.MaxX:F2}, {boundingBox.MaxY:F2}, {boundingBox.MaxZ:F2})");
                            }
                        }
                        catch (Exception meshEx)
                        {
                            meshSummary.AppendLine($"  - Error creating bounding box: {meshEx.Message}");
                        }
                    }
                    else
                    {
                        meshSummary.AppendLine($"  - Element ID: {element.Id} has no valid geometry.");
                    }
                }
            }

            // Perform clash detection
            clashResults.AppendLine("=== CLASH DETECTION RESULTS ===");
            clashResults.AppendLine($"Total elements analyzed: {elementCollisionData.Count}");
            clashResults.AppendLine($"Clash threshold: {clashThreshold} cubic units");
            clashResults.AppendLine();

            var clashCount = 0;
            var totalPairs = 0;

            // Check each pair of elements for clashes using bounding box overlap analysis
            for (int i = 0; i < elementCollisionData.Count; i++)
            {
                for (int j = i + 1; j < elementCollisionData.Count; j++)
                {
                    totalPairs++;
                    var element1 = elementCollisionData[i];
                    var element2 = elementCollisionData[j];

                    // Perform pure C# collision detection
                    var clashInfo = DetectBoundingBoxClash(element1, element2, clashThreshold);
                    if (clashInfo != null)
                    {
                        clashCount++;
                        clashResults.AppendLine($"CLASH #{clashCount}:");
                        clashResults.AppendLine($"  Element 1: {element1.ElementName} (ID: {element1.ElementId})");
                        clashResults.AppendLine($"    - Volume: {element1.Volume:F6} cubic units");
                        clashResults.AppendLine($"    - Bounding Box: ({element1.MinX:F2}, {element1.MinY:F2}, {element1.MinZ:F2}) to ({element1.MaxX:F2}, {element1.MaxY:F2}, {element1.MaxZ:F2})");
                        clashResults.AppendLine($"  Element 2: {element2.ElementName} (ID: {element2.ElementId})");
                        clashResults.AppendLine($"    - Volume: {element2.Volume:F6} cubic units");
                        clashResults.AppendLine($"    - Bounding Box: ({element2.MinX:F2}, {element2.MinY:F2}, {element2.MinZ:F2}) to ({element2.MaxX:F2}, {element2.MaxY:F2}, {element2.MaxZ:F2})");
                        clashResults.AppendLine($"  Clash Details:");
                        clashResults.AppendLine($"    - Type: {clashInfo.ClashType}");
                        clashResults.AppendLine($"    - Intersection Volume: {clashInfo.IntersectionVolume:F6} cubic units");
                        clashResults.AppendLine($"    - Element 1 Overlap: {clashInfo.Element1VolumePercent:F2}% of its volume");
                        clashResults.AppendLine($"    - Element 2 Overlap: {clashInfo.Element2VolumePercent:F2}% of its volume");
                        if (clashInfo.ContactPoints.Any())
                        {
                            clashResults.AppendLine($"    - Intersection Center: ({clashInfo.ContactPoints[0].X:F3}, {clashInfo.ContactPoints[0].Y:F3}, {clashInfo.ContactPoints[0].Z:F3})");
                        }
                        clashResults.AppendLine();
                    }
                }
            }

            if (clashCount == 0)
            {
                clashResults.AppendLine("✅ No clashes detected between the analyzed elements.");
            }
            else
            {
                clashResults.AppendLine($"⚠️  Found {clashCount} clashes out of {totalPairs} element pairs checked.");
            }

            // No cleanup needed for managed clash detection analysis
        }
        catch (Exception ex)
        {
            var errorMessage = $"Application failed with error: {ex.Message}\nStack trace: {ex.StackTrace}";
            Console.WriteLine($"\n{errorMessage}");
            return $"Error during clash detection analysis: {ex.Message}";
        }

        var finalResult = new StringBuilder();
        finalResult.AppendLine("=== CLASH DETECTION ANALYSIS COMPLETED ===");
        finalResult.AppendLine();
        finalResult.AppendLine("ELEMENT GEOMETRY SUMMARY:");
        finalResult.AppendLine(meshSummary.ToString());
        finalResult.AppendLine(clashResults.ToString());
        
        return finalResult.ToString();
    }

    /// <summary>
    /// Creates a bounding box from an Autodesk mesh for clash detection analysis
    /// </summary>
    private static ElementBoundingBox? CreateBoundingBoxFromMesh(dynamic mesh)
    {
        try
        {
            if (mesh?.Vertices == null || mesh.Vertices.Count < 3)
                return null;

            // Calculate bounding box from vertices
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var vertex in mesh.Vertices)
            {
                float x = (float)vertex.X;
                float y = (float)vertex.Y;
                float z = (float)vertex.Z;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                maxZ = Math.Max(maxZ, z);
            }

            // Only ensure minimum size for truly zero-dimension boxes (avoid artificial expansion)
            if (maxX - minX < 0.001f) { minX -= 0.0005f; maxX += 0.0005f; }
            if (maxY - minY < 0.001f) { minY -= 0.0005f; maxY += 0.0005f; }
            if (maxZ - minZ < 0.001f) { minZ -= 0.0005f; maxZ += 0.0005f; }

            return new ElementBoundingBox
            {
                MinX = minX,
                MinY = minY,
                MinZ = minZ,
                MaxX = maxX,
                MaxY = maxY,
                MaxZ = maxZ
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating bounding box: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the element name from the element, with fallback to ID
    /// </summary>
    private static string GetElementName(Autodesk.Data.DataModels.Element element)
    {
        try
        {
            // Try to get a meaningful name from element properties or use the ID
            return !string.IsNullOrEmpty(element.Name) ? element.Name : $"Element_{element.Id}";
        }
        catch
        {
            return $"Element_{element.Id}";
        }
    }

    /// <summary>
    /// Detects clashes between two elements using bounding box overlap analysis
    /// </summary>
    private static ClashInfo? DetectBoundingBoxClash(ElementBoundingBox element1, ElementBoundingBox element2, double threshold)
    {
        try
        {
            // Define a small tolerance to avoid floating point precision issues and touching detection
            const float tolerance = 0.001f;
            
            // Check for AABB (Axis-Aligned Bounding Box) ACTUAL overlap (not just touching)
            // For true overlap, one box's max must be greater than the other's min by more than tolerance
            bool overlapsX = (element1.MaxX - tolerance) > element2.MinX && (element2.MaxX - tolerance) > element1.MinX;
            bool overlapsY = (element1.MaxY - tolerance) > element2.MinY && (element2.MaxY - tolerance) > element1.MinY;
            bool overlapsZ = (element1.MaxZ - tolerance) > element2.MinZ && (element2.MaxZ - tolerance) > element1.MinZ;
            
            if (overlapsX && overlapsY && overlapsZ)
            {
                // Calculate intersection volume (only if there's actual overlap)
                float intersectionX = Math.Min(element1.MaxX, element2.MaxX) - Math.Max(element1.MinX, element2.MinX);
                float intersectionY = Math.Min(element1.MaxY, element2.MaxY) - Math.Max(element1.MinY, element2.MinY);
                float intersectionZ = Math.Min(element1.MaxZ, element2.MaxZ) - Math.Max(element1.MinZ, element2.MinZ);
                
                // Ensure all intersection dimensions are positive (actual overlap)
                if (intersectionX > tolerance && intersectionY > tolerance && intersectionZ > tolerance)
                {
                    double intersectionVolume = intersectionX * intersectionY * intersectionZ;
                    
                    if (intersectionVolume >= threshold)
                    {
                        // Calculate center point of intersection
                        var centerX = (Math.Max(element1.MinX, element2.MinX) + Math.Min(element1.MaxX, element2.MaxX)) / 2;
                        var centerY = (Math.Max(element1.MinY, element2.MinY) + Math.Min(element1.MaxY, element2.MaxY)) / 2;
                        var centerZ = (Math.Max(element1.MinZ, element2.MinZ) + Math.Min(element1.MaxZ, element2.MaxZ)) / 2;
                        
                        // Calculate percentage of each element's volume that's intersecting
                        double element1VolPercent = (intersectionVolume / element1.Volume) * 100;
                        double element2VolPercent = (intersectionVolume / element2.Volume) * 100;
                        
                        return new ClashInfo
                        {
                            ClashType = intersectionVolume > 0.1 ? "Major Intersection" : "Minor Overlap",
                            IntersectionVolume = intersectionVolume,
                            ContactPoints = new List<Vector3> 
                            { 
                                new Vector3(centerX, centerY, centerZ) 
                            },
                            Element1VolumePercent = element1VolPercent,
                            Element2VolumePercent = element2VolPercent
                        };
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting clash: {ex.Message}");
            return null;
        }
    }


}

/// <summary>
/// Represents a bounding box for an element used in clash detection analysis
/// </summary>
internal class ElementBoundingBox
{
    public string ElementId { get; set; } = string.Empty;
    public string ElementName { get; set; } = string.Empty;
    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MinZ { get; set; }
    public float MaxX { get; set; }
    public float MaxY { get; set; }
    public float MaxZ { get; set; }

    public double Volume => Math.Abs((MaxX - MinX) * (MaxY - MinY) * (MaxZ - MinZ));
    
    public Vector3 Center => new Vector3((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);
}

/// <summary>
/// Contains detailed information about a detected element clash
/// </summary>
internal class ClashInfo
{
    public string ClashType { get; set; } = string.Empty;
    public double IntersectionVolume { get; set; }
    public List<Vector3> ContactPoints { get; set; } = new List<Vector3>();
    public double Element1VolumePercent { get; set; }
    public double Element2VolumePercent { get; set; }
}



internal class ElementGroup
{
	internal string id { get; set; } = string.Empty;
	internal string name { get; set; } = string.Empty;
	internal string fileVersionUrn { get; set; } = string.Empty;

	public override string ToString()
	{
		return $"id: {id}, name: {name}, fileVersionUrn: {fileVersionUrn}";
	}
}


internal class Hub
{
   internal string id { get; set; } = string.Empty;
    internal string name { get; set; } = string.Empty;
    internal string dataManagementAPIHubId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"hubId: {id}, hubName: {name}, dataManagementAPIHubId: {dataManagementAPIHubId}";
    }
}

internal class Project
{
    internal string id { get; set; } = string.Empty;
    internal string name { get; set; } = string.Empty;
    internal string dataManagementAPIProjectId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"projectId: {id}, projectName: {name}, dataManagementAPIProjectId: {dataManagementAPIProjectId}";
    }
}	




internal class Element
{
	internal string id { get; set; } = string.Empty;
	internal string name { get; set; } = string.Empty;
	internal List<Property> properties { get; set; } = new List<Property>();

	public override string ToString()
	{
		var externalIdProp = properties.Find(prop => prop.name == "External ID");
		return $"id: {id}, name: {name}, external id: {externalIdProp?.value ?? "N/A"} ";
	}
}

internal class Property
{
	internal string name { get; set; } = string.Empty;
	internal string value { get; set; } = string.Empty;
	public override string ToString()
	{
		return $"name: {name}, value: {value}";
	}
}