using System.Threading.Tasks;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace DecompilationDiffer;

public class Program
{
    public static async Task Main(string[] args)
    {
        var x = new Editor();
        

        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");

        await builder.Build().RunAsync();
    }
}
