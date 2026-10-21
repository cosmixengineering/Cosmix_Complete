using System.Reflection;
using System.Text;
using System.Text.Json;
using Cosmix.Api.Controllers.HR;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Domain.Common.Approvals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

static class HttpChecks
{
    public static async Task<int> Run(StaffTaxonomyRules rules)
    {
        var count=0;void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);count++;}
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var request=new RequestStaffTaxonomy(null,0,new(2026,10,15),null,rules,"synthetic reason","synthetic evidence");
        var json=JsonSerializer.Serialize(request,options);
        var duplicateFlag=json.Replace("\"outsideOvertimeInheritance\":true","\"outsideOvertimeInheritance\":false,\"outsideOvertimeInheritance\":true");
        var result=await Execute(nameof(StaffTaxonomyController.RequestTaxonomy),typeof(RequestStaffTaxonomy),duplicateFlag);
        Check(result.Status==400&&!result.Bound,"raw duplicate category flag rejected before MVC binding");
        var valid=await Execute(nameof(StaffTaxonomyController.RequestTaxonomy),typeof(RequestStaffTaxonomy),json);
        Check(valid.Status==200&&valid.Bound&&valid.Position==0,"valid typed request rewound for real MVC-style binding");
        var decision=JsonSerializer.Serialize(new DecideStaffTaxonomy("AAAAAAAAAAA=","AAAAAAAAAAA=",ApprovalState.Approved,"reason","proof"),options);
        var duplicateDecision=decision.Replace("\"decision\":1","\"decision\":3,\"decision\":1");
        var decided=await Execute(nameof(StaffTaxonomyController.Decide),typeof(DecideStaffTaxonomy),duplicateDecision);
        Check(decided.Status==400&&!decided.Bound,"raw duplicate decision rejected before MVC binding");
        var cancelled=JsonSerializer.Serialize(new CancelStaffTaxonomy("AAAAAAAAAAA=","AAAAAAAAAAA=","reason","proof"),options);
        var cancel=await Execute(nameof(StaffTaxonomyController.Cancel),typeof(CancelStaffTaxonomy),cancelled.Replace("\"reason\":\"reason\"","\"reason\":\"other\",\"reason\":\"reason\""));
        Check(cancel.Status==400&&!cancel.Bound,"raw duplicate cancel reason rejected");
        Check((await Execute(nameof(StaffTaxonomyController.RequestTaxonomy),typeof(RequestStaffTaxonomy),json.Replace("\"expectedRevision\":0","\"ExpectedRevision\":0"))).Status==400,"case mismatched field rejected");
        Check((await Execute(nameof(StaffTaxonomyController.RequestTaxonomy),typeof(RequestStaffTaxonomy),json.Replace("\"expectedRevision\":0","\"expectedRevision\":0,\"unknown\":1"))).Status==400,"unknown field rejected");
        Check((await Execute(nameof(StaffTaxonomyController.RequestTaxonomy),typeof(RequestStaffTaxonomy),json.Replace("\"outsideOvertimeInheritance\":true,",""))).Status==400,"missing boolean rejected before binding");
        Check((await Execute(nameof(StaffTaxonomyController.Decide),typeof(DecideStaffTaxonomy),new string(' ',49154),null)).Status==413,"chunked decision body byte limit");
        Console.WriteLine($"PASS: {count} actual taxonomy controller/filter/MVC-body checks.");return count;
    }
    static async Task<(int Status,bool Bound,long Position)> Execute(string actionName,Type contract,string json,long? declaredLength=-1)
    {
        var http=new DefaultHttpContext();var bytes=Encoding.UTF8.GetBytes(json);http.Request.Body=new MemoryStream(bytes);http.Request.ContentType="application/json";
        http.Request.ContentLength=declaredLength==-1?bytes.Length:declaredLength;
        var action=new ActionContext(http,new RouteData(),new ActionDescriptor());var context=new ResourceExecutingContext(action,[],new List<IValueProviderFactory>());
        var bound=false;var position=-1L;
        async Task<ResourceExecutedContext> Next(){bound=true;position=http.Request.Body.Position;await JsonSerializer.DeserializeAsync(http.Request.Body,contract,new JsonSerializerOptions(JsonSerializerDefaults.Web));return new ResourceExecutedContext(action,[]){Result=new OkResult()};}
        var filter=typeof(StaffTaxonomyController).GetMethod(actionName)!.GetCustomAttributes().OfType<IAsyncResourceFilter>().SingleOrDefault();
        if(filter is null)await Next();else await filter.OnResourceExecutionAsync(context,Next);
        return (context.Result is ObjectResult failed?failed.StatusCode??500:200,bound,position);
    }
}
