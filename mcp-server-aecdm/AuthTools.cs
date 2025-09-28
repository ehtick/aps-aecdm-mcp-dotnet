using ModelContextProtocol.Server;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Web;
using System.Security.Cryptography;
using Autodesk.Data;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class AuthTools
{
	[McpServerTool, Description("Get the token from the user")]
	public static async Task<string> GetToken()
	{
		await GenerateAPSToken();
		return $"Token generated: {Global.AccessToken}";
	}


	public static async Task GenerateAPSToken() {

		Environment.SetEnvironmentVariable("DXSDKEnvironment", "stg");
		var env1 = System.Environment.GetEnvironmentVariable("DXSDKEnvironment");

		//TBD: The current SDK does not support PKCE, so we will use the default setup for now.
		var sdkOptions = new SDKOptionsDefaultSetup
		{
			ConnectorName = "applicationName",
			ClientId = Environment.GetEnvironmentVariable("CLIENT_ID"),
			ClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET"),
			CallBack = Environment.GetEnvironmentVariable("CALLBACK_URL"),
			HostApplicationName = "AECDMSampleApp",
			HostApplicationVersion = "1.0.0",
			ConnectorVersion = "1.0.0",
		}; 
		Global.SDKClient = new Autodesk.Data.Client(sdkOptions);
		Global.AccessToken = Global.SDKClient.SDKOptions.AuthProvider.GetAuthToken();
    }

}