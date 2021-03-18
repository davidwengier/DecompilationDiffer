using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        public string BaseOutput { get; private set; } = "";
        public string Version1Output { get; private set; } = "";
        public string Version2Output { get; private set; } = "";

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

                this.BaseOutput = CompileAndDecompile(_baseCode, "base");
                this.Version1Output = CompileAndDecompile(_version1, "version 1");
                this.Version2Output = CompileAndDecompile(_version2, "version 2");
            }
            catch (Exception ex)
            {
                this.BaseOutput = "Error doing something:\n\n" + ex.ToString();
            }
        }

        private string CompileAndDecompile(string code, string name)
        {
            SyntaxTree? codeTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Regular), "Program.cs");
            var codeCompilation = CSharpCompilation.Create("Program", new SyntaxTree[] { codeTree }, s_references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false));

            var errors = GetErrors("Error compiling " + name + " code:\n\n", codeCompilation.GetDiagnostics());
            if (errors != null)
            {
                return errors;
            }

            var assemblyStream = GetAssemblyStream(codeCompilation, out var rawErrors);
            if (rawErrors is { Length: > 0 } || assemblyStream == null)
            {
                return "Error getting assembly stream for " + name + " code: " + rawErrors;
            }
            using var peFile = new PEFile("", assemblyStream);
            var decompiler = new ICSharpCode.Decompiler.CSharp.CSharpDecompiler(peFile, s_assemblyResolver, _decompilerSettings);
            return decompiler.DecompileWholeModuleAsString();
        }

        private Stream? GetAssemblyStream(Compilation generatorCompilation,out string? errors)
        {
            try
            {
                var generatorStream = new MemoryStream();
                Microsoft.CodeAnalysis.Emit.EmitResult? result = generatorCompilation.Emit(generatorStream);
                if (!result.Success)
                {
                    errors = GetErrors($"Error emitting aseembly:", result.Diagnostics, false);
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

            // CodeBase is obsolete, and it says to use Location, but Location is blank ¯\_(ツ)_/¯
#pragma warning disable SYSLIB0012 // Type or member is obsolete
            foreach (Assembly? reference in refs.Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.CodeBase)))
            {
                Stream? stream = await client.GetStreamAsync($"_framework/{Path.GetFileName(reference.CodeBase)}");
                if (stream is null || reference.FullName is null)
                {
                    continue;
                }
                references.Add((reference.FullName, stream));
            }
#pragma warning restore SYSLIB0012 // Type or member is obsolete

            return references;
        }
    }
}
