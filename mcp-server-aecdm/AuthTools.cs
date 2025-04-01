using ModelContextProtocol.Server;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Web;
using System.Security.Cryptography;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class AuthTools
{
	private const string BASE_URL = "https://developer.api.autodesk.com/aec/graphql";

	[McpServerTool, Description("Get the token from the user")]
	public static async Task<string> GetToken()
	{
		await GenerateAPSToken();
		return $"Token generated: {Global.AccessToken}";
	}
	public static async Task GenerateAPSToken()
	{
		string codeVerifier = RandomString(64);
		string codeChallenge = GenerateCodeChallenge(codeVerifier);
		Global.codeVerifier = codeVerifier;
		//read from environment variables
		Global.ClientId = "rJW5G6ax4vsj2ilF7SZefIAf10Tmi0fCRRqGKItt0IEx6Wmq";
		Global.CallbackURL = "http://localhost:8080/";
		Global.Scopes = "data:read";
		await redirectToLogin(codeChallenge);
	}

	static async Task redirectToLogin(string codeChallenge)
	{
		string[] prefixes =
		{
				Global.CallbackURL
			};

		System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
		{
			FileName = $"https://developer.api.autodesk.com/authentication/v2/authorize?response_type=code&client_id={Global.ClientId}&redirect_uri={HttpUtility.UrlEncode(Global.CallbackURL)}&scope={Global.Scopes}&prompt=login&code_challenge={codeChallenge}&code_challenge_method=S256",
			UseShellExecute = true
		});

		await SimpleListenerExample(prefixes, codeChallenge);
	}

	// This example requires the System and System.Net namespaces.
	static async Task SimpleListenerExample(string[] prefixes, string codeChallenge)
	{
		if (!HttpListener.IsSupported)
		{
			throw new NotSupportedException("HttpListener is not supported in this context!");
		}
		// URI prefixes are required,
		// for example "http://localhost.com:8080".
		if (prefixes == null || prefixes.Length == 0)
			throw new ArgumentException("prefixes");

		// Create a listener.
		HttpListener listener = new HttpListener();
		// Add the prefixes.
		foreach (string s in prefixes)
		{
			listener.Prefixes.Add(s);
		}
		listener.Start();

		//Console.WriteLine("Listening...");
		// Note: The GetContext method blocks while waiting for a request.
		HttpListenerContext context = listener.GetContext();
		HttpListenerRequest request = context.Request;
		// Obtain a response object.
		HttpListenerResponse response = context.Response;

		try
		{
			string authCode = request.Url.Query.ToString().Split('=')[1];
			await GetPKCEToken(authCode);
		}
		catch (Exception ex)
		{
			Console.WriteLine("An error occurred!");
			Console.WriteLine(ex.Message);
		}

		// Construct a response.
		string responseString = "<HTML><BODY> You can move to the form!</BODY></HTML>";
		byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
		// Get a response stream and write the response to it.
		response.ContentLength64 = buffer.Length;
		System.IO.Stream output = response.OutputStream;
		output.Write(buffer, 0, buffer.Length);
		// You must close the output stream.
		output.Close();
		listener.Stop();
	}

	static string RandomString(int length)
	{
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		return new string(Enumerable.Repeat(chars, length)
				.Select(s => s[Global.random.Next(s.Length)]).ToArray());

		//Note: The use of the Random class makes this unsuitable for anything security related, such as creating passwords or tokens.Use the RNGCryptoServiceProvider class if you need a strong random number generator
	}

	static string GenerateCodeChallenge(string codeVerifier)
	{
		var sha256 = SHA256.Create();
		var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
		var b64Hash = Convert.ToBase64String(hash);
		var code = Regex.Replace(b64Hash, "\\+", "-");
		code = Regex.Replace(code, "\\/", "_");
		code = Regex.Replace(code, "=+$", "");
		return code;
	}

	static async Task GetPKCEToken(string authCode)
	{
		try
		{
			var client = new HttpClient();
			var request = new HttpRequestMessage
			{
				Method = HttpMethod.Post,
				RequestUri = new Uri("https://developer.api.autodesk.com/authentication/v2/token"),
				Content = new FormUrlEncodedContent(new Dictionary<string, string>
					{
							{ "client_id", Global.ClientId },
							{ "code_verifier", Global.codeVerifier },
							{ "code", authCode},
							{ "scope", Global.Scopes },
							{ "grant_type", "authorization_code" },
							{ "redirect_uri", Global.CallbackURL }
					}),
			};

			using (var response = await client.SendAsync(request))
			{
				response.EnsureSuccessStatusCode();
				string bodystring = await response.Content.ReadAsStringAsync();
				JObject bodyjson = JObject.Parse(bodystring);
				Global.AccessToken = bodyjson["access_token"].Value<string>();
				Global.RefreshToken = bodyjson["refresh_token"].Value<string>();
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("An error occurred!");
			Console.WriteLine(ex.Message);
		}
	}
}