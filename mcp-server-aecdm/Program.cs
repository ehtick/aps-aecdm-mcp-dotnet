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

var app = builder.Build();

await app.RunAsync();