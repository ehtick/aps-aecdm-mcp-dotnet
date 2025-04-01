using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text;
using mcp_server_aecdm;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Web;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer()
		.WithStdioServerTransport()
		.WithToolsFromAssembly();

builder.Services.AddSingleton(_ =>
{
	var client = new HttpClient() { BaseAddress = new Uri("https://api.weather.gov") };
	client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("weather-tool", "1.0"));
	return client;
});

var app = builder.Build();

await app.RunAsync();