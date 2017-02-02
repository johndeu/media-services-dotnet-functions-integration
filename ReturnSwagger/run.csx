using System.Net;
using System.Net.Http.Headers;
using System.IO;

public static HttpResponseMessage Run(HttpRequestMessage req, TraceWriter log)
{
    var response = new HttpResponseMessage(HttpStatusCode.OK);
    var stream = new FileStream(Path.Combine(GetScriptPath(), 
        @"ReturnSwagger\swagger.json"), FileMode.Open);
    response.Content = new StreamContent(stream);
    response.Content.Headers.ContentType = 
        new MediaTypeHeaderValue("application/json");
    return response;
}

private static string GetScriptPath()
    => Path.Combine(GetEnvironmentVariable("HOME"), @"site\wwwroot");

private static string GetEnvironmentVariable(string name)
    => System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
