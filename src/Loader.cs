using System.IO;
using System.Reflection;
using Iface.Oik.Tm.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Iface.Oik.CommonCalc;

public static class Loader
{
  public static bool AddWorkers(this IServiceCollection services)
  {
    var fileProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
    foreach (var file in fileProvider.GetDirectoryContents(string.Empty))
    {
      var content = string.Empty;
      
      using (var stream = file.CreateReadStream())
      using (var reader = new StreamReader(stream))
      {
        content = reader.ReadToEnd();
      }
      services.AddSingleton<IHostedService>(provider => new Worker(provider.GetService<IOikDataApi>(),
                                                                   file.Name,
                                                                   content));
    }

    return true;
  }
}