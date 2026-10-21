#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Security.Cryptography;
using System.Text;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Infrastructure.Files;

var count=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;}
var scanner=new ClamAvEmployeeFileScanner(new ClamAvScannerOptions { Enabled=true,Host="127.0.0.1",Port=13310,TimeoutSeconds=30,MaxSizeBytes=1048576 });
var clean=Encoding.UTF8.GetBytes("Cosmix synthetic employee document scanner acceptance; no employee information.");
var cleanHash=SHA256.HashData(clean);
var cleanResult=await scanner.ScanAsync(clean,CancellationToken.None);
Check(cleanResult.State==DocumentScanStatus.Clean,"Real configured ClamAV returns Clean for bounded synthetic plain text");
Check(cleanResult.ScannerVersion.StartsWith("ClamAV ",StringComparison.Ordinal),"Actual scanner version retained");
Check(SHA256.HashData(clean).SequenceEqual(cleanHash),"Scanner does not mutate caller-owned clean bytes");
// Standard inert antimalware test bytes are submitted directly over INSTREAM;
// no test object is written to disk or associated with an employee.
var marker=Encoding.ASCII.GetBytes("X5O!P%@AP[4"+"\\PZX54(P^)7CC)7}"+"$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
var markerHash=SHA256.HashData(marker);
var rejected=await scanner.ScanAsync(marker,CancellationToken.None);
Check(rejected.State==DocumentScanStatus.Rejected,"Real ClamAV rejects the standard inert antimalware test marker");
Check(rejected.ScannerVersion==cleanResult.ScannerVersion,"Both scans retain the same real engine/definition version");
Check(SHA256.HashData(marker).SequenceEqual(markerHash),"Scanner does not mutate caller-owned test bytes");
var disabled=await new ClamAvEmployeeFileScanner(new()).ScanAsync(clean,CancellationToken.None);
Check(disabled.State==DocumentScanStatus.Failed,"Disabled scanner never reports Clean");
var invalid=await scanner.ScanAsync([],CancellationToken.None);
Check(invalid.State==DocumentScanStatus.Failed,"Empty input is denied before network scanning");
var tooLarge=await scanner.ScanAsync(new byte[1048577],CancellationToken.None);
Check(tooLarge.State==DocumentScanStatus.Failed,"Over-limit input is denied before network scanning");
using(var cancelled=new CancellationTokenSource()){
 cancelled.Cancel();try{await scanner.ScanAsync(clean,cancelled.Token);throw new Exception("Cancelled scan succeeded");}
 catch(OperationCanceledException){Check(true,"Cancelled request remains cancelled");}
}
CryptographicOperations.ZeroMemory(marker);CryptographicOperations.ZeroMemory(clean);
Console.WriteLine($"PASS {count} actual local ClamAV scanner checks; engine={cleanResult.ScannerVersion}; in-memory synthetic samples, no employee/database/carrier request");
