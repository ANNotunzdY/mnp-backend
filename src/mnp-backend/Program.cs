using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.CloudSearchDomain;
using Amazon.CloudSearchDomain.Model;
using Amazon.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add AWS Lambda support. When application is run in Lambda Kestrel is swapped out as the web server with Amazon.Lambda.AspNetCoreServer. This
// package will act as the webserver translating request and responses between the Lambda event source and ASP.NET Core.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();


app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => "Welcome to running ASP.NET Core Minimal API on AWS Lambda");

app.MapPost("/add", async (List<List<string>> videos) =>
{
    AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();
    string tableName = "SmileVideos";

    foreach (var video in videos)
    {
        if (video.Count != 2)
        {
            return Results.BadRequest("Each item must contain exactly 2 elements: [id, title]");
        }
        
        var id = video[0];
        var title = video[1];

        var request = new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "id", new AttributeValue { S = id } },
                { "title", new AttributeValue { S = title } }
            }
        };

        await dynamoDbClient.PutItemAsync(request);
    }

    return Results.Ok();
});

app.MapGet("/update", async () =>
{
    AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();
    AmazonCloudSearchDomainClient searchClient = new AmazonCloudSearchDomainClient("http://search-mnp-backend-q4zcqdt6443zg2cxp7n4zpsd2e.ap-northeast-1.cloudsearch.amazonaws.com");
    var tableName = "SmileVideos";
    var scanRequest = new ScanRequest
    {
        TableName = tableName
    };
    var scanResponse = await dynamoDbClient.ScanAsync(scanRequest);
    var items = new List<Dictionary<string, object>>();

    foreach (var item in scanResponse.Items)
    {
        var dictItem = new Dictionary<string, object>();
	dictItem["type"] = "add";
	var fields = new Dictionary<string, string>();
        foreach (var kvp in item)
        {
	    if (kvp.Key == "id") {
                dictItem[kvp.Key] = kvp.Value.S;
            } else {
                fields[kvp.Key] = kvp.Value.S;
            }
        }
	dictItem["fields"] = fields;
        items.Add(dictItem);
    }

    using var memoryStream = new MemoryStream();
    
    await JsonSerializer.SerializeAsync(memoryStream, items);
   
    // メモリーストリームの位置をリセット
    memoryStream.Position = 0;

    var uploadRequest = new UploadDocumentsRequest
    {
        Documents = memoryStream,
        ContentType = "application/json"
    };

    var uploadResponse = await searchClient.UploadDocumentsAsync(uploadRequest);

    return Results.Ok(items);
});

app.MapGet("/all", async () =>
{
    AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();
    var tableName = "SmileVideos";
    var scanRequest = new ScanRequest
    {
        TableName = tableName
    };
    var scanResponse = await dynamoDbClient.ScanAsync(scanRequest);
    var items = new List<Dictionary<string, object>>();

    foreach (var item in scanResponse.Items)
    {
        var dictItem = new Dictionary<string, object>();
        foreach (var kvp in item)
        {
	    if (kvp.Key == "id") {
                dictItem[kvp.Key] = kvp.Value.S;
            } else {
                dictItem[kvp.Key] = new string[] {kvp.Value.S};
            }
        }
        items.Add(dictItem);
    }

    return Results.Ok(items);
});

app.MapGet("/search", async (string q) =>
{
    AmazonCloudSearchDomainClient searchClient = new AmazonCloudSearchDomainClient("http://search-mnp-backend-q4zcqdt6443zg2cxp7n4zpsd2e.ap-northeast-1.cloudsearch.amazonaws.com");

    var searchRequest = new SearchRequest
    {
        Query = q,
        QueryParser = "simple",
        Return = "_all_fields"
    };

    var searchResponse = await searchClient.SearchAsync(searchRequest);
    var searchResults = new List<Dictionary<string, object>>();

    foreach (var hit in searchResponse.Hits.Hit)
    {
        var resultItem = new Dictionary<string, object>
        {
            { "id", hit.Id }
        };

        foreach (var field in hit.Fields)
        {
            resultItem[field.Key] = field.Value;
        }

        searchResults.Add(resultItem);
    }

    return Results.Ok(searchResults);
});

app.Run();
