using IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApp.Services;

public sealed class SystemBrowser : IBrowser
{
    public int Port { get; }
    private readonly string? _path;

    public SystemBrowser(int? port = null, string? path = null)
    {
        _path = path;
        Port = port ?? GetRandomUnusedPort();
    }

    private int GetRandomUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{Port}/");
        listener.Start();

        Debug.WriteLine($"=== Opening browser with URL: {options.StartUrl}");
        OpenBrowser(options.StartUrl);

        var context = await listener.GetContextAsync();
        
        var formData = GetRequestPostData(context.Request);

        var response = context.Response;
        string responseString = @"
<html>
<head>
    <title>Login Successful</title>
</head>
<body>
    <h1>Login successful!</h1>
    <p>You can close this window and return to the application.</p>
    <script>window.close();</script>
</body>
</html>";

        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        var output = response.OutputStream;
        await output.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        output.Close();
        listener.Stop();

        var values = context.Request.QueryString;
        var error = values.Get("error");
        if (!string.IsNullOrEmpty(error))
        {
            return new BrowserResult
            {
                ResultType = BrowserResultType.UnknownError,
                Error = error
            };
        }

        var code = values.Get("code");
        var state = values.Get("state");

        return new BrowserResult
        {
            ResultType = BrowserResultType.Success,
            Response = $"code={code}&state={state}"
        };
    }

    private string GetRequestPostData(HttpListenerRequest request)
    {
        if (!request.HasEntityBody)
        {
            return string.Empty;
        }

        using var body = request.InputStream;
        using var reader = new StreamReader(body, request.ContentEncoding);
        return reader.ReadToEnd();
    }

    public static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Workaround for cross-platform launch
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }
}
