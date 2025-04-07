using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Web;

namespace mcp_server_aecdm
{
	[McpServerToolType]
	public static class ViewerTool
	{
		[McpServerTool, Description("Show the elements in viewer based on their External Ids")]
		public static async Task<string> HighLightElements([Description("Array of external ids that must be highlighted!")] string[] externalIds)
		{
			if (Global._webSocket != null && Global._webSocket.State == WebSocketState.Open)
			{
				byte[] buffer = Encoding.UTF8.GetBytes(string.Join(",", externalIds));
				await Global._webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			return "Elements highlighted!";
		}

		[McpServerTool, Description("Render one model with Autodesk Viewer.")]
		public static async Task<string> RenderModel([Description("urn used to render the model with Viewer")] string fileVersionUrn)
		{
			//base64 encode the urn
			string urnBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileVersionUrn));

			//return an html page as string
			string htmlContent = @"
<!DOCTYPE html>
<html>

<head>
  <title>Autodesk Platform Services Claude Viewer Template</title>
  <!-- Autodesk Platform Services Viewer files -->
  <link rel=""stylesheet"" href=""https://developer.api.autodesk.com/modelderivative/v2/viewers/7.*/style.min.css""
        type=""text/css"">
  <script src=""https://developer.api.autodesk.com/modelderivative/v2/viewers/7.*/viewer3D.min.js""></script>
</head>

<body onload=""initAPSViewer()"">
  <div id=""apsViewer""></div>
</body>
<script>

  let viewer = null;
  //REPLACE THE TOKEN HERE
  var _access_token = '" + Global.AccessToken+@"';
  //REPLACE THE URN HERE
  var _urn = '"+ urnBase64 + @"';

	let socket = new WebSocket('ws://localhost:8081');
  socket.onmessage = function(event) {
      var externalIds = event.data.split(',');
			viewer.model.getExternalIdMapping((externalIdsDictionary) => {
        let dbids = [];
        externalIds.forEach(externalId => {
          let dbid = externalIdsDictionary[externalId];
          if (!!dbid)
            dbids.push(dbid);
        });
        viewer.isolate(dbids);
        viewer.fitToView();
      }, console.log)
  };

  async function initAPSViewer() {
    const options = {
      env: 'AutodeskProduction',
      accessToken: _access_token,
      isAEC: true
    };

    Autodesk.Viewing.Initializer(options, () => {

      const div = document.getElementById('apsViewer');

			const config = { extensions: ['Autodesk.DocumentBrowser'] }

      viewer = new Autodesk.Viewing.Private.GuiViewer3D(div, config);
      viewer.start();
      viewer.setTheme(""light-theme"");
      Autodesk.Viewing.Document.load(`urn:${_urn}`, doc => {
        var viewables = doc.getRoot().getDefaultGeometry();
        viewer.loadDocumentNode(doc, viewables).then(onLoadFinished);
      });
    });
  }

</script>


</html>";
			// Start a simple HTTP server
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add("http://localhost:8080/");
			listener.Start();

			Task.Run(() =>
			{
				while (true)
				{
					HttpListenerContext context = listener.GetContext();
					HttpListenerResponse response = context.Response;
					byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
					response.ContentLength64 = buffer.Length;
					response.OutputStream.Write(buffer, 0, buffer.Length);
					response.OutputStream.Close();
				}
			});

			// Start a WebSocket server
			Task.Run(async () =>
			{
				HttpListener webSocketListener = new HttpListener();
				webSocketListener.Prefixes.Add("http://localhost:8081/");
				webSocketListener.Start();

				while (true)
				{
					HttpListenerContext context = await webSocketListener.GetContextAsync();
					if (context.Request.IsWebSocketRequest)
					{
						HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
						Global._webSocket = webSocketContext.WebSocket;
					}
				}
			});

			// Open the default web browser
			Process.Start(new ProcessStartInfo("http://localhost:8080/") { UseShellExecute = true });

			return "Viewer has been initialized in the browser!";
		}
	}
}
