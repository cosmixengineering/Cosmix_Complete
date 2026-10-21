#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Cosmix.Api.Authentication;

// Inspect executable async code, without connecting to SQL or starting the host.
// Reintroducing a permission/grant writer anywhere reachable from verification fails.
var assembly = typeof(OtpDeliveryWorker).Assembly;
var local = assembly.GetType("Cosmix.Api.Development.LocalTesting")!;
var entry = local.GetMethod("VerifyDatabaseAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(x => x.FieldType == typeof(OpCode)).Select(x => (OpCode)x.GetValue(null)!)
    .ToDictionary(x => unchecked((ushort)x.Value));
var seen = new HashSet<MethodBase>();
var queue = new Queue<MethodBase>();
queue.Enqueue(entry);
var forbidden = new List<string>();
while (queue.TryDequeue(out var method))
{
    if (!seen.Add(method)) continue;
    var state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
    if (state is not null) queue.Enqueue(state.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance)!);
    var il = method.GetMethodBody()?.GetILAsByteArray();
    if (il is null) continue;
    for (var p = 0; p < il.Length;)
    {
        ushort code = il[p++];
        if (code == 0xfe) code = (ushort)(0xfe00 | il[p++]);
        var opcode = opcodes[code];
        if (opcode.OperandType == OperandType.InlineMethod)
        {
            var called = method.Module.ResolveMethod(BitConverter.ToInt32(il, p));
            if (called is not null)
            {
                var name = called.Name;
                if (called.DeclaringType?.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true &&
                    (name.StartsWith("SaveChanges", StringComparison.Ordinal) ||
                    name.StartsWith("ExecuteSql", StringComparison.Ordinal) ||
                    name.StartsWith("ExecuteUpdate", StringComparison.Ordinal) ||
                    name.StartsWith("ExecuteDelete", StringComparison.Ordinal) ||
                    name is "Add" or "AddRange" or "Update" or "Remove"))
                    forbidden.Add($"{method.DeclaringType?.FullName}.{method.Name} -> {called.DeclaringType?.FullName}.{name}");
                if (called.DeclaringType?.Assembly == assembly &&
                    called.DeclaringType.FullName!.StartsWith("Cosmix.Api.Development.LocalTesting", StringComparison.Ordinal))
                    queue.Enqueue(called);
            }
        }
        p += opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, p),
            _ => 4
        };
    }
}
if (forbidden.Count != 0)
    throw new Exception("Startup verification can mutate persisted grants: " + string.Join("; ", forbidden));
if (seen.Count < 2) throw new Exception("Async startup body was not inspected.");
Console.WriteLine($"PASS: {seen.Count} compiled startup methods inspected; no reachable persistence writer. No SQL or host started.");
