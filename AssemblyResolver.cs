using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using ICSharpCode.Decompiler.Metadata;

namespace DecompilationDiffer;

internal class AssemblyResolver : IAssemblyResolver
{
    private readonly List<PEFile> _peFiles;
    private readonly List<Stream> _streams;

    public AssemblyResolver()
    {
        _peFiles = [];
        _streams = [];
        foreach (var reference in Net80.ReferenceInfos.All)
        {
            var stream = new MemoryStream(reference.ImageBytes);
            _peFiles.Add(new PEFile(reference.FileName, stream));
            stream.Seek(0, SeekOrigin.Begin);
            _streams.Add(stream);
        }
    }

    public MetadataFile? Resolve(IAssemblyReference reference)
    {
        return _peFiles.FirstOrDefault(r => r.FullName == reference.FullName);
    }

    public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference)
    {
        return Task.FromResult(_peFiles.FirstOrDefault(r => r.FullName == reference.FullName) as MetadataFile);
    }

    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
    {
        return null;
    }

    public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName)
    {
        return Task.FromResult<MetadataFile?>(null);
    }

    internal IEnumerable<Stream> GetAllStreams()
    {
        return _streams;
    }
}
