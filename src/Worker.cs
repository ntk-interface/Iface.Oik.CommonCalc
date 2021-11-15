using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Iface.Oik.Tm.Helpers;
using Iface.Oik.Tm.Interfaces;
using Jint;
using Microsoft.Extensions.Hosting;

namespace Iface.Oik.CommonCalc;

public class Worker : BackgroundService
{
  private readonly IOikDataApi _api;
  private readonly string      _name;
  private readonly string      _script;

  private readonly Engine _engine;
  private          int    _scriptTimeout = 2000;

  private readonly List<TmGroup>  _groups   = new();
  private readonly List<TmStatus> _statuses = new();
  private readonly List<TmAnalog> _analogs  = new();


  public Worker(IOikDataApi api, string name, string script)
  {
    _api    = api;
    _name   = name;
    _script = script;

    InitGroups();

    _engine = new Engine()
              .SetValue("TmFlagUnreliable",      TmFlags.Unreliable)
              .SetValue("TmFlagManuallyBlocked", TmFlags.ManuallyBlocked)
              .SetValue("TmFlagManuallySet",     TmFlags.ManuallySet)
              .SetValue("TmFlagLevelA",          TmFlags.LevelA)
              .SetValue("TmFlagLevelB",          TmFlags.LevelB)
              .SetValue("TmFlagLevelC",          TmFlags.LevelC)
              .SetValue("TmFlagLevelD",          TmFlags.LevelD)
              .SetValue("GetGroupArray",         new Func<int, int[]>(GetGroupArray))
              .SetValue("GetTmStatus",           new Func<int, int>(GetTmStatus))
              .SetValue("GetTmAnalog",           new Func<int, float>(GetTmAnalog))
              .SetValue("IsTmStatusOn",          new Func<int, bool>(IsTmStatusOn))
              .SetValue("IsTmStatusOff",         new Func<int, bool>(IsTmStatusOff))
              .SetValue("SetTmStatus",           new Action<int, int>(SetTmStatus))
              .SetValue("SetTmStatusOn",         new Action<int>(SetTmStatusOn))
              .SetValue("SetTmStatusOff",        new Action<int>(SetTmStatusOff))
              .SetValue("SetTmAnalog",           new Action<int, float>(SetTmAnalog))
              .SetValue("RaiseTmStatusFlags",    new Action<int, TmFlags>(RaiseTmStatusFlags))
              .SetValue("ClearTmStatusFlags",    new Action<int, TmFlags>(ClearTmStatusFlags))
              .SetValue("RaiseTmAnalogFlags",    new Action<int, TmFlags>(RaiseTmAnalogFlags))
              .SetValue("ClearTmAnalogFlags",    new Action<int, TmFlags>(ClearTmAnalogFlags))
              .SetValue("LogDebug",              new Action<string>(LogDebug));

    _engine.Execute(_script);
  }


  private void InitGroups()
  {
    _groups.AddRange(new[]
    {
      new TmGroup("DateTimeIsAllowed"),
      new TmGroup("DateTime"),
      new TmGroup("TsOn"),
      new TmGroup("TsOff"),
      new TmGroup("TsSwitch"),
      new TmGroup("TsTrolley"),
    });

    AddGroupTmStatus(0, 24, 1, 1);

    AddGroupTmAnalog(1, 24, 1, 1);
    AddGroupTmAnalog(1, 24, 1, 2);
    AddGroupTmAnalog(1, 24, 1, 3);
    AddGroupTmAnalog(1, 24, 1, 4);
    AddGroupTmAnalog(1, 24, 1, 5);
    AddGroupTmAnalog(1, 24, 1, 6);
    AddGroupTmAnalog(1, 24, 1, 7);

    AddGroupTmStatus(2, 0, 1, 1);
    AddGroupTmStatus(2, 0, 1, 3);
    AddGroupTmStatus(2, 0, 1, 5);

    AddGroupTmStatus(3, 0, 1, 2);
    AddGroupTmStatus(3, 0, 1, 4);

    AddGroupTmStatus(4, 22, 1,  1);
    AddGroupTmStatus(4, 22, 13, 1);
    AddGroupTmStatus(4, 22, 14, 1);

    AddGroupTmStatus(5, 22, 1,  11);
    AddGroupTmStatus(5, 22, 13, 11);
    AddGroupTmStatus(5, 22, 14, 11);
  }


  private void AddGroupTmStatus(int group, int ch, int rtu, int point)
  {
    var statusIdx = InitTmStatus(ch, rtu, point);
    _groups[group].TagsIndexes.Add(statusIdx);
  }


  private int InitTmStatus(int ch, int rtu, int point)
  {
    var existingTmStatusIndex = FindTmStatus(ch, rtu, point);
    if (existingTmStatusIndex >= 0)
    {
      return existingTmStatusIndex;
    }
    var tmStatus = new TmStatus(ch, rtu, point);
    _statuses.Add(tmStatus);
    return _statuses.Count - 1;
  }


  private int FindTmStatus(int ch, int rtu, int point)
  {
    var index = 0;
    foreach (var tmStatus in _statuses)
    {
      if (tmStatus.TmAddr.Equals(ch, rtu, point))
      {
        return index;
      }
      index++;
    }
    return -1;
  }


  private void AddGroupTmAnalog(int group, int ch, int rtu, int point)
  {
    var statusIdx = InitTmAnalog(ch, rtu, point);
    _groups[group].TagsIndexes.Add(statusIdx);
  }


  private int InitTmAnalog(int ch, int rtu, int point)
  {
    var existingTmAnalogIndex = FindTmAnalog(ch, rtu, point);
    if (existingTmAnalogIndex >= 0)
    {
      return existingTmAnalogIndex;
    }
    var tmAnalog = new TmAnalog(ch, rtu, point);
    _analogs.Add(tmAnalog);
    return _analogs.Count - 1;
  }


  private int FindTmAnalog(int ch, int rtu, int point)
  {
    var index = 0;
    foreach (var tmAnalog in _analogs)
    {
      if (tmAnalog.TmAddr.Equals(ch, rtu, point))
      {
        return index;
      }
      index++;
    }
    return -1;
  }


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await Task.Delay(500, stoppingToken); // такое асинхронное ожидание даёт хосту возможность завершить инициализацию

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var sw = Stopwatch.StartNew();
        await DoWork();
        Tms.PrintDebug($"Скрипт \"{_name}\" рассчитан за {sw.ElapsedMilliseconds} мс");
      }
      catch (Exception ex)
      {
        Tms.PrintDebug($"Ошибка при расчете скрипта \"{_name}\": {ex.Message}");
      }
      await Task.Delay(_scriptTimeout, stoppingToken);
    }
  }


  private async Task DoWork()
  {
    await _api.UpdateStatuses(_statuses);
    await _api.UpdateAnalogs(_analogs);

    _engine.Invoke("DoWork");
  }


  private int[] GetGroupArray(int idx)
  {
    return _groups[idx].TagsIndexes.ToArray();
  }


  private bool IsTmStatusOn(int idx)
  {
    return GetTmStatus(idx) > 0;
  }


  private bool IsTmStatusOff(int idx)
  {
    return GetTmStatus(idx) == 0;
  }


  private int GetTmStatus(int idx)
  {
    var tmStatus = _statuses.ElementAtOrDefault(idx);
    if (tmStatus == null)
    {
      return -1;
    }
    return tmStatus.Status;
  }


  private float GetTmAnalog(int idx)
  {
    var tmAnalog = _analogs.ElementAtOrDefault(idx);
    if (tmAnalog == null)
    {
      return -1;
    }
    return tmAnalog.Value;
  }


  private void SetTmStatus(int idx, int status)
  {
    var tmStatus = _statuses.ElementAtOrDefault(idx);
    if (tmStatus == null)
    {
      return;
    }
    var (ch, rtu, point) = tmStatus.TmAddr.GetTuple();
    _api.SetStatus(ch, rtu, point, status);
  }


  private void SetTmStatusOn(int idx)
  {
    SetTmStatus(idx, 1);
  }


  private void SetTmStatusOff(int idx)
  {
    SetTmStatus(idx, 0);
  }


  private void SetTmAnalog(int idx, float value)
  {
    var tmAnalog = _analogs.ElementAtOrDefault(idx);
    if (tmAnalog == null)
    {
      return;
    }
    var (ch, rtu, point) = tmAnalog.TmAddr.GetTuple();
    _api.SetAnalog(ch, rtu, point, value);
  }


  private void RaiseTmStatusFlags(int idx, TmFlags flags)
  {
    var tmStatus = _statuses.ElementAtOrDefault(idx);
    if (tmStatus == null)
    {
      return;
    }
    var (ch, rtu, point) = tmStatus.TmAddr.GetTuple();
    _api.SetTagFlags(new TmStatus(ch, rtu, point), flags);
  }


  private void ClearTmStatusFlags(int idx, TmFlags flags)
  {
    var tmStatus = _statuses.ElementAtOrDefault(idx);
    if (tmStatus == null)
    {
      return;
    }
    var (ch, rtu, point) = tmStatus.TmAddr.GetTuple();
    _api.ClearTagFlags(new TmStatus(ch, rtu, point), flags);
  }


  private void RaiseTmAnalogFlags(int idx, TmFlags flags)
  {
    var tmAnalog = _analogs.ElementAtOrDefault(idx);
    if (tmAnalog == null)
    {
      return;
    }
    var (ch, rtu, point) = tmAnalog.TmAddr.GetTuple();
    _api.SetTagFlags(new TmAnalog(ch, rtu, point), flags);
  }


  private void ClearTmAnalogFlags(int idx, TmFlags flags)
  {
    var tmAnalog = _analogs.ElementAtOrDefault(idx);
    if (tmAnalog == null)
    {
      return;
    }
    var (ch, rtu, point) = tmAnalog.TmAddr.GetTuple();
    _api.ClearTagFlags(new TmAnalog(ch, rtu, point), flags);
  }


  private string GetExpressionResult(string expression)
  {
    return _api.GetExpressionResult(expression).GetAwaiter().GetResult();
  }


  private bool TryGetExpressionResult(string expression, out float value)
  {
    var expressionResult = GetExpressionResult(expression);
    if (!float.TryParse(expressionResult, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
    {
      LogDebug(expressionResult);
      return false;
    }
    return true;
  }


  private void LogDebug(string message)
  {
    Tms.PrintDebug($"Отладочное сообщение скрипта \"{_name}\": {message}");
  }


  private class TmGroup
  {
    public string Name { get; }

    public List<int> TagsIndexes { get; } = new();


    public TmGroup(string name)
    {
      Name = name;
    }
  }
}