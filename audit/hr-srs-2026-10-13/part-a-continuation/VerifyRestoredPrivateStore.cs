#:project C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Infrastructure.Files;
var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cosmix","PartAContinuation","FileRestore20261013");
var options=JsonSerializer.Deserialize<PrivateFileStorageOptions>(File.ReadAllBytes(Path.Combine(root,"restored-options.json")))??throw new Exception("Missing restored provider configuration");
var store=new EncryptedPrivateFileStore(options);var bytes=Encoding.UTF8.GetBytes("Synthetic private evidence restore test; contains no employee information.");var checks=0;
var before=Directory.GetFiles(options.Directory).Length;
var reference=await store.WriteAsync(bytes,"part-a-acceptance/private-restore",default);
var read=await store.ReadAsync(reference,"part-a-acceptance/private-restore",default);
Check(read.SequenceEqual(bytes),"actual restored key decrypts new encrypted object exactly");CryptographicOperations.ZeroMemory(read);
Check(Directory.GetFiles(options.Directory).Length==before+1,"immutable encrypted object added only to restored test directory");
await Denied(()=>store.ReadAsync(reference,"part-a-acceptance/other-purpose",default),"cross-purpose read denied");
var wrongKeyPath=Path.Combine(root,"Restored","Keys","synthetic-wrong-key.bin");
if(File.Exists(wrongKeyPath))throw new Exception("Preserve prior test evidence; do not overwrite");
var wrongKey=RandomNumberGenerator.GetBytes(32);await File.WriteAllBytesAsync(wrongKeyPath,wrongKey);CryptographicOperations.ZeroMemory(wrongKey);
var wrongStore=new EncryptedPrivateFileStore(options with {KeyFile=wrongKeyPath,DecryptionKeyFiles=[]});
await Denied(()=>wrongStore.ReadAsync(reference,"part-a-acceptance/private-restore",default),"incorrect key cannot decrypt evidence");
var scanner=new ClamAvEmployeeFileScanner(new(){Enabled=true,Host="127.0.0.1",Port=13310,TimeoutSeconds=30,MaxSizeBytes=1048576});
read=await store.ReadAsync(reference,"part-a-acceptance/private-restore",default);
try{Check((await scanner.ScanAsync(read,default)).State==DocumentScanStatus.Clean,"real scanner accepts decrypted synthetic restored evidence");}finally{CryptographicOperations.ZeroMemory(read);}
if(System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint=>endpoint.Port==13311))
 throw new Exception("The bounded unavailable-transport port is occupied; do not contact another service");
var unavailable=new ClamAvEmployeeFileScanner(new(){Enabled=true,Host="127.0.0.1",Port=13311,TimeoutSeconds=1});
Check((await unavailable.ScanAsync(bytes,default)).State==DocumentScanStatus.Failed,"unavailable actual scanner transport fails closed");
Check(SHA256.HashData(bytes).SequenceEqual(Convert.FromHexString(reference.Sha256)),"provider operations preserve caller sample bytes");
CryptographicOperations.ZeroMemory(bytes);
Console.WriteLine($"PASS {checks} actual restored-key/private-file/scanner checks; original objects unchanged; no employee/SQL/carrier request");
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
async Task Denied(Func<Task<byte[]>> action,string name){try{var content=await action();CryptographicOperations.ZeroMemory(content);}catch(AuthenticationFailure e)when(e.StatusCode==503){checks++;return;}throw new Exception(name);}
