using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Basic.Reference.Assemblies;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        internal void Run()
        {
            try
            {
                if (s_assemblyResolver == null)
                {
                    s_assemblyResolver = new AssemblyResolver(Net60.References.All);
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
            SyntaxTree? codeTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Regular).WithLanguageVersion(LanguageVersion.Preview), "Program.cs");

            var outputKind = codeTree.GetCompilationUnitRoot().Members.Any(m => m is GlobalStatementSyntax)
                ? OutputKind.ConsoleApplication
                : OutputKind.DynamicallyLinkedLibrary;

            var codeCompilation = CSharpCompilation.Create("Program", new SyntaxTree[] { codeTree }, ReferenceAssemblies.Net60, new CSharpCompilationOptions(outputKind, concurrentBuild: false));

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

        private static Stream? GetAssemblyStream(Compilation generatorCompilation, out string? errors)
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
    }
}
