using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text;
using mcp_server_aecdm;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Web;
using mcp_server_aecdm.Tools;
using System.Reflection;

// Check if we should run in HTTP mode
var cmdArgs = Environment.GetCommandLineArgs();
var httpMode = cmdArgs.Contains("--http") || cmdArgs.Contains("--web");

if (httpMode)
{
    // HTTP mode - run web server on http://127.0.0.1:4000/mcp/
    var builder = WebApplication.CreateBuilder(args);
    
    // Configure the server to listen on the specified URL
    builder.WebHost.UseUrls("http://127.0.0.1:4000");
    
    // Add MCP server services for HTTP mode
    builder.Services.AddMcpServer()
        .WithToolsFromAssembly();
    
    var app = builder.Build();
    
    // Add MCP protocol endpoints
    app.MapPost("/mcp/", async (HttpContext context) =>
    {
        try
        {
            // Read the JSON-RPC request
            using var reader = new StreamReader(context.Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            
            // Parse and handle the MCP request
            var jsonRequest = JObject.Parse(requestBody);
            var method = jsonRequest["method"]?.ToString();
            var idToken = jsonRequest["id"];
            
            // Convert JToken id to proper type (string, number, or null)
            object? id = null;
            if (idToken != null)
            {
                if (idToken.Type == JTokenType.String)
                    id = idToken.ToString();
                else if (idToken.Type == JTokenType.Integer)
                    id = idToken.ToObject<int>();
                else if (idToken.Type == JTokenType.Float)
                    id = idToken.ToObject<double>();
            }
            
            // Handle the request and create proper JSON-RPC response
            if (string.IsNullOrEmpty(method))
            {
                var errorResponse = new
                {
                    jsonrpc = "2.0",
                    id = id,
                    error = new { code = -32600, message = "Invalid Request", data = "Missing method" }
                };
                context.Response.ContentType = "application/json";
                return Results.Json(errorResponse);
            }
            
            object? result = null;
            object? error = null;
            
            switch (method)
            {
                case "initialize":
                    result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { },
                            resources = new { }
                        },
                        serverInfo = new
                        {
                            name = "mcp-server-aecdm",
                            version = "1.0.0"
                        }
                    };
                    break;
                    
                case "tools/list":
                    result = new
                    {
                        tools = new object[]
                        {
                            new
                            {
                                name = "GetHubs",
                                description = "Get the ACC hubs from the user",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new { },
                                    required = new string[] { }
                                }
                            },
                            new
                            {
                                name = "GetProjects",
                                description = "Get the ACC projects from one hub",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        hubId = new { type = "string", description = "The hub ID" }
                                    },
                                    required = new[] { "hubId" }
                                }
                            },
                            new
                            {
                                name = "GetFiles",
                                description = "Get the Designs/Models/ElementGroups from one project",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        projectId = new { type = "string", description = "The project ID" }
                                    },
                                    required = new[] { "projectId" }
                                }
                            },
                            new
                            {
                                name = "GetElementsByCategory",
                                description = "Get the Elements from the ElementGroup/Design using a category filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        elementGroupUrn = new { type = "string", description = "The element group URN" },
                                        category = new { type = "string", description = "Element category filter" }
                                    },
                                    required = new[] { "elementGroupUrn", "category" }
                                }
                            },
                            new
                            {
                                name = "GetElementsByFilter",
                                description = "Get the Elements from the ElementGroup/Design using a filter. The filter is a string that will be used in the GraphQL query. For example: 'property.name.category'=='Walls' and 'property.name.Element Context'=='Instance'",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        elementGroupUrn = new { type = "string", description = "The element group URN" },
                                        filter = new { type = "string", description = "GraphQL filter expression" }
                                    },
                                    required = new[] { "elementGroupUrn", "filter" }
                                }
                            },
                            new
                            {
                                name = "ExportIfc",
                                description = "Export Ifc file for the selected element",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        elementUrn = new { type = "string", description = "The element URN" }
                                    },
                                    required = new[] { "elementUrn" }
                                }
                            },
                            new
                            {
                                name = "PerformClashDetection",
                                description = "Perform accurate clash detection analysis between selected building elements",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        elementUrn1 = new { type = "string", description = "First element URN" },
                                        elementUrn2 = new { type = "string", description = "Second element URN" }
                                    },
                                    required = new[] { "elementUrn1", "elementUrn2" }
                                }
                            },
                            new
                            {
                                name = "GetToken",
                                description = "Get the token from the user",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new { },
                                    required = new string[] { }
                                }
                            },
                            new
                            {
                                name = "UploadFileToDocs",
                                description = "Upload the file to Autodesk Docs",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        localFilePath = new { type = "string", description = "File path to upload" },
                                        hubId = new { type = "string", description = "Data Management API Hub Id" },
                                        projectId = new { type = "string", description = "Data Management API Project Id" },
                                        folderId = new { type = "string", description = "Folder id (optional)" }
                                    },
                                    required = new[] { "localFilePath", "hubId", "projectId" }
                                }
                            },
                            new
                            {
                                name = "HighLightElements",
                                description = "Show the elements in viewer based on their External Ids",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        externalIds = new { type = "array", description = "Array of external ids that must be highlighted" }
                                    },
                                    required = new[] { "externalIds" }
                                }
                            },
                            new
                            {
                                name = "RenderModel",
                                description = "Render one model with Autodesk Viewer",
                                inputSchema = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        fileVersionUrn = new { type = "string", description = "URN used to render the model with Viewer" }
                                    },
                                    required = new[] { "fileVersionUrn" }
                                }
                            }
                        }
                    };
                    break;
                    
                case "tools/call":
                    result = await HandleToolCall(jsonRequest);
                    break;
                    
                default:
                    error = new { code = -32601, message = "Method not found" };
                    break;
            }
            
            // Create proper JSON-RPC response
            object response;
            if (error != null)
            {
                response = new
                {
                    jsonrpc = "2.0",
                    id = id,
                    error = error
                };
            }
            else
            {
                response = new
                {
                    jsonrpc = "2.0",
                    id = id,
                    result = result
                };
            }
            
            context.Response.ContentType = "application/json";
            return Results.Json(response);
        }
        catch (Exception ex)
        {
            var errorResponse = new
            {
                jsonrpc = "2.0",
                id = (object?)null,
                error = new { code = -32700, message = "Parse error", data = ex.Message }
            };
            return Results.Json(errorResponse);
        }
    });
    
    // Add info endpoint (GET)
    app.MapGet("/mcp/", () => Results.Json(new
    {
        message = "MCP Server HTTP Interface",
        server = "mcp-server-aecdm",
        version = "1.0.0",
        transport = "http",
        endpoint = "http://127.0.0.1:4000/mcp/",
        status = "running",
        protocol = "JSON-RPC 2.0",
        methods = new[] { "initialize", "tools/list", "tools/call" }
    }));
    
    app.MapGet("/mcp/health", () => Results.Json(new { status = "healthy", timestamp = DateTime.UtcNow }));
    
    Console.WriteLine("MCP Server running in HTTP mode on http://127.0.0.1:4000/mcp/");
    await app.RunAsync();
}
else
{
    // Default STDIO mode - original MCP server behavior
var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer()
		.WithStdioServerTransport()
		.WithToolsFromAssembly();

var app = builder.Build();

    Console.WriteLine("MCP Server running in STDIO mode");
await app.RunAsync();
}

// Tool execution handler for HTTP mode
static async Task<object> HandleToolCall(JObject request)
{
    try
    {
        var paramsObj = request["params"] as JObject;
        var toolName = paramsObj?["name"]?.ToString();
        var arguments = paramsObj?["arguments"] as JObject;
        
        if (string.IsNullOrEmpty(toolName))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Tool name is required"
                    }
                }
            };
        }
        
        // Execute the tool based on the name
        string result = toolName switch
        {
            // AECDMTools
            "GetHubs" => await mcp_server_aecdm.Tools.AECDMTools.GetHubs(),
            "GetProjects" => await mcp_server_aecdm.Tools.AECDMTools.GetProjects(arguments?["hubId"]?.ToString() ?? ""),
            "GetFiles" => await mcp_server_aecdm.Tools.AECDMTools.GetElementGroupsByProject(arguments?["projectId"]?.ToString() ?? ""),
            "GetElementsByCategory" => await mcp_server_aecdm.Tools.AECDMTools.GetElementsByElementGroupWithCategoryFilter(
                arguments?["elementGroupUrn"]?.ToString() ?? "",
                arguments?["category"]?.ToString() ?? ""),
            "GetElementsByFilter" => "Error: GetElementsByFilter tool is not available. Use GetElementsByCategory instead.",
            "ExportIfc" => await mcp_server_aecdm.Tools.AECDMTools.ExportIfcForElements(
                new string[] { arguments?["elementUrn"]?.ToString() ?? "" }),
            "PerformClashDetection" => await mcp_server_aecdm.Tools.AECDMTools.ClashDetectForElements(
                new string[] { arguments?["elementUrn1"]?.ToString() ?? "", arguments?["elementUrn2"]?.ToString() ?? "" }),
            
            // AuthTools
            "GetToken" => await mcp_server_aecdm.Tools.AuthTools.GetToken(),
            
            // DMTools
            "UploadFileToDocs" => await mcp_server_aecdm.Tools.DMTools.UploadFileToDocs(
                arguments?["localFilePath"]?.ToString() ?? "",
                arguments?["hubId"]?.ToString() ?? "",
                arguments?["projectId"]?.ToString() ?? "",
                arguments?["folderId"]?.ToString()),
            
            // ViewerTool
            "HighLightElements" => await mcp_server_aecdm.ViewerTool.HighLightElements(
                arguments?["externalIds"]?.ToObject<string[]>() ?? new string[0]),
            "RenderModel" => await mcp_server_aecdm.ViewerTool.RenderModel(
                arguments?["fileVersionUrn"]?.ToString() ?? ""),
            
            _ => $"Error: Unknown tool '{toolName}'"
        };
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = result
                }
            }
        };
    }
    catch (Exception ex)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Error executing tool: {ex.Message}"
                }
            }
        };
    }
}

