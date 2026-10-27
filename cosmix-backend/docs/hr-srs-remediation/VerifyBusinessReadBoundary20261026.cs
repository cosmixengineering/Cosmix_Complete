using System;
using System.Reflection;
using Microsoft.AspNetCore.Http;
var type = typeof(Cosmix.Api.Authentication.OtpDeliveryWorker).Assembly.GetType("Cosmix.Api.Development.AuthOnlyTestingBoundary")!;
var method = type.GetMethod("AllowsBusinessRead", BindingFlags.Static | BindingFlags.NonPublic);
if (method is null) throw new Exception("Business read boundary missing");
int checks=0;
void Check(bool value) { if(!value) throw new Exception("Business read guard failed"); checks++; }
bool Allows(string verb,string path) => (bool)method.Invoke(null,new object[]{verb,path})!;
string[] routes=["/api/v1/leaves/mine/benefits?yearFrom=2026-01-01","/api/v1/leaves/mine/benefits?yearFrom=not-a-date","/api/v1/leaves/admin/employees/35d19922-70a8-4c80-8578-e1029360e9fc/benefits?yearFrom=2026-01-01","/api/v1/leaves/admin/recovery-work/queue","/api/v1/leaves/mine/recovery-work/26102600-0000-4000-8000-000000000001","/api/v1/hr/vehicles/fuel-claims?employeeId=35d19922-70a8-4c80-8578-e1029360e9fc&page=1&pageSize=10","/api/v1/hr/vehicles/trips/26102600-0000-4000-8000-000000000001/attribution-correction-context","/api/v1/hr/vehicles/fuel-claims/26102600-0000-4000-8000-000000000001/reconciliation-context"];
foreach(var profile in new string?[]{null,"HumaHrAcceptance20261019C02","HumaHrAcceptance20261026Auth01","unknown"}) {
 Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST",profile);
 foreach(var route in routes) Check(!Allows("GET",route));
}
Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST","HumaHrAcceptance20261026Read01");
foreach(var route in routes) {
 Check(Allows("GET",route));
 foreach(var verb in new[]{"POST","PUT","PATCH","DELETE"}) Check(!Allows(verb,route));
 Check(!Allows("GET",route+"&extra=true")); Check(!Allows("GET",route+"/extra"));
 Check(!Allows("GET","https://remote.invalid"+route));
}
foreach(var route in new[]{"/api/v1/leaves","/api/v1/hr/vehicles","/api/v1/hr/employees","/api/v1/leaves/admin/employees/other/benefits?yearFrom=2026-01-01"}) Check(!Allows("GET",route));
Console.WriteLine($"PASS {checks} business read allowlist checks; no host or SQL.");
