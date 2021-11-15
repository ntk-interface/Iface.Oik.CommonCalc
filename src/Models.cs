using System.Collections.Generic;

namespace Iface.Oik.CommonCalc;

public class ScriptTask
{
  public bool         IsDisabled      { get; }
  public string       ScriptName      { get; }
  public int          PeriodInSeconds { get; }
  public List<string> GroupNames      { get; } = new();


  public ScriptTask(string isEnabledString, string scriptName, string periodInSecondsString, params string[] groupNames)
  {
    if (isEnabledString == "0")
    {
      IsDisabled = true;
    }

    ScriptName = scriptName;

    if (int.TryParse(periodInSecondsString, out var periodInSeconds))
    {
      PeriodInSeconds = periodInSeconds;
    }

    GroupNames.AddRange(groupNames);
  }
}