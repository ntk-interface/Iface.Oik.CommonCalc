using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Iface.Oik.Tm.Helpers;
using Iface.Oik.Tm.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Iface.Oik.CommonCalc;

public static class Loader
{
  public static string ConfigPath { get; set; }


  public static bool AddWorkers(this IServiceCollection services)
  {
    if (!File.Exists(ConfigPath))
    {
      Tms.PrintError("Не найден файл конфигурации");
      return false;
    }
    
    var embeddedScripts = LoadEmbeddedScripts();

    var workersCount = 0;
    foreach (var task in GetTasksFromConfigFile())
    {
      if (task.IsDisabled)
      {
        continue;
      }
      if (task.PeriodInSeconds == 0)
      {
        Tms.PrintError($"Не могу разрешить скрипт \"{task.ScriptName}\" с нулевым периодом запуска");
        continue;
      }
      if (!embeddedScripts.TryGetValue(task.ScriptName, out var scriptContent) &&
          !TryLoadLocalScript(task.ScriptName, out scriptContent))
      {
        Tms.PrintError($"Не найден скрипт \"{task.ScriptName}\"");
        continue;
      }
      services.AddSingleton<IHostedService>(provider => new Worker(provider.GetService<IOikDataApi>(),
                                                                   task,
                                                                   scriptContent));
      workersCount++;
    }

    if (workersCount == 0)
    {
      Tms.PrintError("Не найдено ни одной задачи расчета");
      return false;
    }

    Tms.PrintMessage($"Всего задач расчета: {workersCount}");
    return true;
  }


  private static Dictionary<string, string> LoadEmbeddedScripts()
  {
    var scripts = new Dictionary<string, string>();
    
    var fileProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
    foreach (var file in fileProvider.GetDirectoryContents(string.Empty))
    {
      using (var stream = file.CreateReadStream())
      using (var reader = new StreamReader(stream))
      {
        scripts.Add(file.Name, reader.ReadToEnd());
      }
    }

    return scripts;
  }


  private static bool TryLoadLocalScript(string path, out string scriptContent)
  {
    if (!File.Exists(path))
    {
      scriptContent = string.Empty;
      return false;
    }
    scriptContent = File.ReadAllText(path);
    return true;
  }


  private static List<ScriptTask> GetTasksFromConfigFile()
  {
    return XDocument.Load(ConfigPath)
                    .Descendants("tmsccTask")
                    .Select(xmlTask => new ScriptTask(xmlTask.Attribute("enab")?.Value,
                                                      xmlTask.Attribute("tmsccAlgo")?.Value,
                                                      xmlTask.Attribute("tmsccPeriod")?.Value,
                                                      xmlTask.Attribute("tmsccParm1")?.Value,
                                                      xmlTask.Attribute("tmsccParm2")?.Value,
                                                      xmlTask.Attribute("tmsccParm3")?.Value,
                                                      xmlTask.Attribute("tmsccParm4")?.Value,
                                                      xmlTask.Attribute("tmsccParm5")?.Value,
                                                      xmlTask.Attribute("tmsccParm6")?.Value,
                                                      xmlTask.Attribute("tmsccParm7")?.Value,
                                                      xmlTask.Attribute("tmsccParm8")?.Value,
                                                      xmlTask.Attribute("tmsccParm9")?.Value,
                                                      xmlTask.Attribute("tmsccParm10")?.Value))
                    .ToList();
  }
}