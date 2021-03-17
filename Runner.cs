using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#nullable enable

namespace DecompilationDiffer
{
    internal class Runner
    {
        private readonly DecompilerSettings _decompilerSettings = new DecompilerSettings(ICSharpCode.Decompiler.CSharp.LanguageVersion.CSharp1)
        {
            ArrayInitializers = false,
            AutomaticEvents = false,
            DecimalConstants = false,
            FixedBuffers = false,
            UsingStatement = false,
            SwitchStatementOnString = false,
            LockStatement = false,
            ForStatement = false,
            ForEachStatement = false,
            SparseIntegerSwitch = false,
            DoWhileStatement = false,
            StringConcat = false,
            UseRefLocalsForAccurateOrderOfEvaluation = true,
            InitAccessors = true,
            FunctionPointers = true,
            NativeIntegers = true
        };
        private static List<MetadataReference>? s_references;
        private static AssemblyResolver? s_assemblyResolver;
        private readonly string _baseCode;
        private readonly string _version1;
        private readonly string _version2;

        public string ErrorText { get; private set; } = "";
        public string Version1Output { get; private set; } = "";
        public string Version2Output { get; private set; } = "";
        public string BaseOutput { get; private set; } = "";

        public Runner(string baseCode, string version1, string version2)
        {
            _baseCode = baseCode;
            _version1 = version1;
            _version2 = version2;
        }

        internal async Task Run(string baseUri)
        {
            try
            {
                if (s_references == null)
                {
                    s_assemblyResolver = new AssemblyResolver(await GetReferenceStreams(baseUri));
                    s_references = GetReferences(s_assemblyResolver);
                }

                this.BaseOutput = "";
                this.Version1Output = "";
                this.Version2Output = "";
                this.ErrorText = "";

                if (string.IsNullOrWhiteSpace(_baseCode) || string.IsNullOrWhiteSpace(_version1))
                {
                    this.ErrorText = "Need more input!";
                    return;
                }

                this.BaseOutput = CompileAndDecompile(_baseCode, out string errors);
                if (errors is not null)
                {
                    this.ErrorText = errors;
                    return;
                }
                this.Version1Output = CompileAndDecompile(_version1, out errors);
                if (errors is not null)
                {
                    this.ErrorText = errors;
                    return;
                }
                this.Version2Output = CompileAndDecompile(_version2, out errors);
                if (errors is not null)
                {
                    this.ErrorText = errors;
                    return;
                }
            }
            catch (Exception ex)
            {
                this.ErrorText = "Error doing something: " + ex.ToString();
            }
        }

        private string? CompileAndDecompile(string code, out string? errors)
        {
            SyntaxTree? codeTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Regular), "Program.cs");
            var codeCompilation = CSharpCompilation.Create("Program", new SyntaxTree[] { codeTree }, s_references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            errors = GetErrors("Error(s) compiling program:", codeCompilation.GetDiagnostics());
            if (errors != null)
            {
                return null;
            }

            var assemblyStream = GetAssemblyStream(codeCompilation, "Code", out var rawErrors);
            if (rawErrors is { Length: > 0 } || assemblyStream == null)
            {
                errors = "Error getting assembly stream: " + rawErrors;
                return null;
            }
            using var peFile = new PEFile("", assemblyStream);
            var decompiler = new ICSharpCode.Decompiler.CSharp.CSharpDecompiler(peFile, s_assemblyResolver, _decompilerSettings);
            return decompiler.DecompileWholeModuleAsString();
        }

        private Stream? GetAssemblyStream(Compilation generatorCompilation, string name, out string? errors)
        {
            try
            {
                var generatorStream = new MemoryStream();
                Microsoft.CodeAnalysis.Emit.EmitResult? result = generatorCompilation.Emit(generatorStream);
                if (!result.Success)
                {
                    errors = GetErrors($"Error emitting {name}:", result.Diagnostics, false);
                    return null;
                }
                generatorStream.Seek(0, SeekOrigin.Begin);
                errors = null;
                return generatorStream;
            }
            catch (Exception ex)
            {
                errors = ex.ToString();
                return null;
            }
        }

        private static string? GetErrors(string header, IEnumerable<Diagnostic> diagnostics, bool errorsOnly = true)
        {
            IEnumerable<Diagnostic>? errors = diagnostics.Where(d => !errorsOnly || d.Severity == DiagnosticSeverity.Error);

            if (!errors.Any())
            {
                return null;
            }

            return header + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, errors);
        }

        private static List<MetadataReference> GetReferences(AssemblyResolver assemblyResolver)
        {
            var references = new List<MetadataReference>();

            foreach (Stream stream in assemblyResolver.GetAllStreams())
            {
                stream.Seek(0, SeekOrigin.Begin);
                references.Add(MetadataReference.CreateFromStream(stream));
            }

            return references;
        }

        private static async Task<List<(string, Stream)>> GetReferenceStreams(string baseUri)
        {
            Assembly[]? refs = AppDomain.CurrentDomain.GetAssemblies();
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUri)
            };

            var references = new List<(string, Stream)>();

            foreach (Assembly? reference in refs.Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location)))
            {
                Stream? stream = await client.GetStreamAsync($"_framework/_bin/{reference.Location}");
                if (stream is null) continue;
                references.Add((reference.FullName, stream));

            }

            return references;
        }
    }
}
