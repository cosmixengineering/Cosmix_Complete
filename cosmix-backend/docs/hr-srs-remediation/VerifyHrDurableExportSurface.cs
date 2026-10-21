using System.Reflection;
using Cosmix.Api.Controllers.HR;

var routes = typeof(HrReportsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
    .SelectMany(m => m.GetCustomAttributesData().Where(a => a.AttributeType.Name == "HttpGetAttribute")
        .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)).ToArray();
if (!routes.Contains("exports/{id:guid}") || !routes.Contains("exports/{id:guid}/download"))
    throw new Exception("RED: rich-HR durable status and authorized download routes are absent.");
Console.WriteLine("PASS durable HR status/download routes present.");
