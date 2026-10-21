#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

if (args.Length is < 2 or > 3) throw new Exception("Supply source repository, fresh binary directory and optional --application-only.");
var repository = Path.GetFullPath(args[0]); var binaries = Path.GetFullPath(args[1]);
var applicationOnly = args.Length == 3 && args[2] == "--application-only";
if (args.Length == 3 && !applicationOnly) throw new Exception("Unknown proof mode.");
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; }
var cases = new (string Path, string Assembly, string Type)[] {
    ("src/Cosmix.Domain/HR/Policies/AdvancedAttendanceRules.cs", "Cosmix.Domain", "Cosmix.Domain.HR.Policies.AdvancedAttendanceRulesCodec"),
    ("src/Cosmix.Application/HR/Policies/Advanced/AdvancedPolicyContracts.cs", "Cosmix.Application", "Cosmix.Application.HR.Policies.Advanced.IAdvancedAttendancePolicyReader"),
    ("src/Cosmix.Application/HR/Policies/Advanced/AdvancedPolicyReader.cs", "Cosmix.Application", "Cosmix.Application.HR.Policies.Advanced.AdvancedPolicyReader"),
    ("src/Cosmix.Application/HR/Policies/Advanced/AdvancedPolicyService.cs", "Cosmix.Application", "Cosmix.Application.HR.Policies.Advanced.AdvancedPolicyService"),
    ("src/Cosmix.Application/HR/Policies/Advanced/AdvancedPolicyRegistration.cs", "Cosmix.Application", "Cosmix.Application.HR.Policies.Advanced.IAdvancedPolicyPublicationRepository"),
    ("src/Cosmix.Infrastructure/Repositories/HR/AdvancedPolicyRepository.cs", "Cosmix.Infrastructure", "Cosmix.Infrastructure.Repositories.HR.AdvancedPolicyRepository"),
    ("src/Cosmix.Infrastructure/Repositories/HR/AdvancedPolicyInfrastructureRegistration.cs", "Cosmix.Infrastructure", "Cosmix.Infrastructure.Repositories.HR.AdvancedPolicyInfrastructureRegistration"),
    ("src/Cosmix.Api/Controllers/HR/AdvancedAttendancePoliciesController.cs", "Cosmix.Api", "Cosmix.Api.Controllers.HR.AdvancedAttendancePoliciesController")
}.Where(x => !applicationOnly || x.Assembly is "Cosmix.Domain" or "Cosmix.Application").ToArray();
foreach (var group in cases.GroupBy(x => x.Assembly)) {
    var dll = Path.Combine(binaries, group.Key + ".dll"); var pdb = Path.ChangeExtension(dll, ".pdb");
    Check(File.Exists(dll) && File.Exists(pdb), "supplied binary and portable PDB exist: " + group.Key);
    var dllBytes = File.ReadAllBytes(dll); var pdbBytes = File.ReadAllBytes(pdb);
    using var pe = new PEReader(new MemoryStream(dllBytes));
    Check(pe.HasMetadata, "actual PE contains managed metadata: " + group.Key);
    var metadata = pe.GetMetadataReader();
    Check(metadata.GetString(metadata.GetAssemblyDefinition().Name) == group.Key, "actual assembly identity: " + group.Key);
    using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(pdbBytes));
    var pdbReader = pdbProvider.GetMetadataReader();
    var debug = pe.ReadDebugDirectory(); var codeView = debug.Single(x => x.Type == DebugDirectoryEntryType.CodeView);
    var header = pdbReader.DebugMetadataHeader ?? throw new Exception("Portable PDB metadata header unavailable.");
    var codeViewData = pe.ReadCodeViewDebugDirectoryData(codeView);
    Check(codeViewData.Guid == new Guid(header.Id.AsSpan()[..16]), "PDB belongs to actual PE binary: " + group.Key);
    var checksumEntry = debug.Single(x => x.Type == DebugDirectoryEntryType.PdbChecksum);
    var checksum = pe.ReadPdbChecksumDebugDirectoryData(checksumEntry);
    Check(checksum.AlgorithmName == "SHA256" && checksum.Checksum.AsSpan().SequenceEqual(CompilerChecksum(pdbBytes)),
        "actual PE validates companion portable PDB compiler checksum: " + group.Key);
    var types = metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle)).Select(type =>
        metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name)).ToHashSet(StringComparer.Ordinal);
    foreach (var item in group) {
        Check(types.Contains(item.Type), "actual binary contains source hook: " + item.Type);
        var source = Path.GetFullPath(Path.Combine(repository, item.Path));
        var documents = pdbReader.Documents.Select(handle => pdbReader.GetDocument(handle)).Where(document =>
            string.Equals(Path.GetFullPath(pdbReader.GetString(document.Name)), source, StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(documents.Length == 1, "actual PDB pins exact source path: " + item.Path);
        var document = documents.Single();
        Check(pdbReader.GetGuid(document.HashAlgorithm) == new Guid("8829d00f-11b8-4213-878b-770e8597ac16"), "document uses SHA256: " + item.Path);
        Check(pdbReader.GetBlobBytes(document.Hash).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))),
            "compiled document checksum equals frozen current source: " + item.Path);
    }
    Console.WriteLine($"BINARY: {group.Key}; MVID={metadata.GetGuid(metadata.GetModuleDefinition().Mvid)}; SHA256={Convert.ToHexString(SHA256.HashData(dllBytes))}; path={dll}");
}
Console.WriteLine($"PASS: {checks} C01 actual PE/PDB/source assertions; {cases.Length} documents; mode={(applicationOnly ? "Application-only (Infrastructure/API unverified)" : "all four product assemblies")}; no SQL or host.");

static byte[] CompilerChecksum(byte[] pdb)
{
    // Deterministic portable PDB hashes are calculated before writing the 20-byte content ID.
    using var stream = new MemoryStream(pdb); using var reader = new BinaryReader(stream);
    if (reader.ReadUInt32() != 0x424a5342) throw new Exception("Invalid portable PDB metadata signature.");
    reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32();
    var versionLength = reader.ReadUInt32(); stream.Position += versionLength;
    reader.ReadUInt16(); var streams = reader.ReadUInt16();
    for (var i = 0; i < streams; i++) {
        var offset = reader.ReadUInt32(); reader.ReadUInt32(); var name = new List<byte>();
        byte next; while ((next = reader.ReadByte()) != 0) name.Add(next);
        stream.Position = (stream.Position + 3) & ~3L;
        if (System.Text.Encoding.ASCII.GetString(name.ToArray()) != "#Pdb") continue;
        var content = pdb.ToArray(); content.AsSpan(checked((int)offset), 20).Clear(); return SHA256.HashData(content);
    }
    throw new Exception("Portable PDB content-ID stream unavailable.");
}
