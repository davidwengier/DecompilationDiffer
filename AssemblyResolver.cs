using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using ICSharpCode.Decompiler.Metadata;

namespace DecompilationDiffer
{
    internal class AssemblyResolver : IAssemblyResolver
    {
        private readonly List<PEFile> _peFiles;
        private readonly List<Stream> _streams;

        public AssemblyResolver(IEnumerable<Net60.ReferenceInfo> references)
        {
            _peFiles = new List<PEFile>();
            _streams = new List<Stream>();
            foreach (var reference in references)
            {
                var stream = new MemoryStream(reference.ImageBytes);
                _peFiles.Add(new PEFile(reference.FileName, stream));
                stream.Seek(0, SeekOrigin.Begin);
                _streams.Add(stream);
            }
        }

        public PEFile? Resolve(IAssemblyReference reference)
        {
            return _peFiles.FirstOrDefault(r => r.FullName == reference.FullName);
        }

        public Task<PEFile?> ResolveAsync(IAssemblyReference reference)
        {
            return Task.FromResult(_peFiles.FirstOrDefault(r => r.FullName == reference.FullName));
        }

        public PEFile? ResolveModule(PEFile mainModule, string moduleName)
        {
            return null;
        }

        public Task<PEFile?> ResolveModuleAsync(PEFile mainModule, string moduleName)
        {
            return Task.FromResult<PEFile?>(null);
        }

        internal IEnumerable<Stream> GetAllStreams()
        {
            return _streams;
        }
    }
}
