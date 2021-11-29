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
  private readonly ScriptTask  _scriptTask;
  private readonly string      _scriptContent;

  private int _period;

  private bool _isErrorFlagRaised;

  private readonly Engine _engine;

  private readonly Dictionary<int, TmStatusGroup> _statusGroups = new();
  private readonly Dictionary<int, TmAnalogGroup> _analogGroups = new();


  public Worker(IOikDataApi api, ScriptTask scriptTask, string scriptContent)
  {
    _api           = api;
    _scriptTask    = scriptTask;
    _scriptContent = scriptContent;

    _name   = _scriptTask.ScriptName;
    _period = _scriptTask.PeriodInSeconds * 1000;

    _engine = new Engine()
              .SetValue("TmFlagUnreliable",        TmFlags.Unreliable)
              .SetValue("TmFlagManuallyBlocked",   TmFlags.ManuallyBlocked)
              .SetValue("TmFlagManuallySet",       TmFlags.ManuallySet)
              .SetValue("TmFlagLevelA",            TmFlags.LevelA)
              .SetValue("TmFlagLevelB",            TmFlags.LevelB)
              .SetValue("TmFlagLevelC",            TmFlags.LevelC)
              .SetValue("TmFlagLevelD",            TmFlags.LevelD)
              .SetValue("InitTmStatusGroupInput",  new Func<int, int[][]>(InitTmStatusGroupInput))
              .SetValue("InitTmStatusGroupOutput", new Func<int, int[][]>(InitTmStatusGroupOutput))
              .SetValue("InitTmAnalogGroupInput",  new Func<int, int[][]>(InitTmAnalogGroupInput))
              .SetValue("InitTmAnalogGroupOutput", new Func<int, int[][]>(InitTmAnalogGroupOutput))
              .SetValue("GetTmStatus",             new Func<int[], int>(GetTmStatus))
              .SetValue("GetTmAnalog",             new Func<int[], float>(GetTmAnalog))
              .SetValue("GetTmAnalogRetro",        new Func<int[], long, long, int?, float[]>(GetTmAnalogRetro))
              .SetValue("GetTmAnalogImpulseArchiveAverage",
                        new Func<int[], long, long, int?, float[]>(GetTmAnalogImpulseArchiveAverage))
              .SetValue("IsTmStatusOn",         new Func<int[], bool>(IsTmStatusOn))
              .SetValue("IsTmStatusOff",        new Func<int[], bool>(IsTmStatusOff))
              .SetValue("IsTmStatusFlagRaised", new Func<int[], TmFlags, bool>(IsTmStatusFlagRaised))
              .SetValue("IsTmAnalogFlagRaised", new Func<int[], TmFlags, bool>(IsTmAnalogFlagRaised))
              .SetValue("SetTmStatus",          new Action<int[], int>(SetTmStatus))
              .SetValue("SetTmStatusOn",        new Action<int[]>(SetTmStatusOn))
              .SetValue("SetTmStatusOff",       new Action<int[]>(SetTmStatusOff))
              .SetValue("SetTmAnalog",          new Action<int[], float>(SetTmAnalog))
              .SetValue("RaiseTmStatusFlags",   new Action<int[], TmFlags>(RaiseTmStatusFlags))
              .SetValue("ClearTmStatusFlags",   new Action<int[], TmFlags>(ClearTmStatusFlags))
              .SetValue("RaiseTmAnalogFlags",   new Action<int[], TmFlags>(RaiseTmAnalogFlags))
              .SetValue("ClearTmAnalogFlags",   new Action<int[], TmFlags>(ClearTmAnalogFlags))
              .SetValue("GetPeriod",            new Func<int>(GetPeriod))
              .SetValue("OverridePeriod",       new Action<int>(OverridePeriod))
              .SetValue("ThrowException",       new Action<string>(ThrowException))
              .SetValue("IsErrorFlagRaised",    new Func<bool>(IsErrorFlagRaised))
              .SetValue("ClearErrorFlag",       new Action(ClearErrorFlag))
              .SetValue("LogError",             new Action<string>(LogError))
              .SetValue("LogDebug",             new Action<string>(LogDebug));
  }


  public override Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      _engine.Execute(_scriptContent);
      return base.StartAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      Tms.PrintError($"Ошибка при инициализации скрипта \"{_name}\": {ex.Message}");
      return Task.CompletedTask;
    }
  }


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      var sw = Stopwatch.StartNew();
      try
      {
        await DoWork();
        Tms.PrintDebug($"Скрипт \"{_name}\" рассчитан за {sw.ElapsedMilliseconds} мс");
      }
      catch (Exception ex)
      {
        Tms.PrintDebug($"Ошибка при расчете скрипта \"{_name}\": {ex.Message}");
      }

      await Task.Delay(_period, stoppingToken);
    }
  }


  private async Task DoWork()
  {
    _isErrorFlagRaised = false;
    
    foreach (var group in _statusGroups.Values.Where(g => g.IsUpdating))
    {
      await _api.UpdateStatuses(group.Statuses);

      if (group.Statuses.Any(s => s.IsUnreliable || s.IsInvalid || s.IsS2Failure))
      {
        _isErrorFlagRaised = true;
      }
    }
    foreach (var group in _analogGroups.Values.Where(g => g.IsUpdating))
    {
      await _api.UpdateAnalogs(group.Analogs);

      if (group.Analogs.Any(a => a.IsUnreliable || a.IsInvalid))
      {
        _isErrorFlagRaised = true;
      }
    }

    _engine.Invoke("DoWork");
  }


  private int[][] InitTmStatusGroup(int groupIdx, bool isUpdating)
  {
    var groupName = _scriptTask.GroupNames.ElementAtOrDefault(groupIdx);
    if (string.IsNullOrEmpty(groupName))
    {
      throw new Exception($"Не найдена требуемая группа сигналов в конфигурации: {groupIdx}");
    }
    var tmStatuses = new List<TmStatus>();
    if (groupName.StartsWith("@"))
    {
      isUpdating = false;
      foreach (var statusString in groupName[1..].Split(';'))
      {
        if (!short.TryParse(statusString, NumberStyles.Any, CultureInfo.InvariantCulture, out var status))
        {
          throw new Exception($"Недопустимое значение в группе: {statusString}");
        }
        tmStatuses.Add(new TmStatus(254, 255, 65535) { Status = status, IsInit = true });
      }
    }
    else if (groupName.StartsWith("#TC"))
    {
      foreach (var tmAddrString in groupName.Split(';'))
      {
        if (!TmAddr.TryParse(tmAddrString, out var tmAddr, TmType.Status))
        {
          throw new Exception($"Недопустимый адрес сигнала: {tmAddrString}");
        }
        tmStatuses.Add(new TmStatus(tmAddr));
      }
    }
    else
    {
      var groupStatuses = _api.GetTagsByGroup(TmType.Status, groupName)
                              .GetAwaiter()
                              .GetResult()
                              ?.Cast<TmStatus>()
                              .ToList();
      if (groupStatuses == null)
      {
        throw new Exception($"Ошибка загрузки данных группы: \"{groupName}\"");
      }
      tmStatuses.AddRange(groupStatuses);
    }
    _statusGroups.Add(groupIdx, new TmStatusGroup(groupName, isUpdating, tmStatuses));

    return tmStatuses.Select((_, idx) => new[] { groupIdx, idx }).ToArray();
  }


  private int[][] InitTmStatusGroupInput(int idx)
  {
    return InitTmStatusGroup(idx, isUpdating: true);
  }


  private int[][] InitTmStatusGroupOutput(int idx)
  {
    return InitTmStatusGroup(idx, isUpdating: false);
  }


  private int[][] InitTmAnalogGroup(int groupIdx, bool isUpdating)
  {
    var groupName = _scriptTask.GroupNames.ElementAtOrDefault(groupIdx);
    if (string.IsNullOrEmpty(groupName))
    {
      throw new Exception($"Не найдена требуемая группа измерений в конфигурации: {groupIdx}");
    }

    var tmAnalogs = new List<TmAnalog>();
    if (groupName.StartsWith("@"))
    {
      isUpdating = false;
      foreach (var valueString in groupName[1..].Split(';'))
      {
        if (!float.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
          throw new Exception($"Недопустимое значение в группе: {valueString}");
        }
        tmAnalogs.Add(new TmAnalog(254, 255, 65535) { Value = value, IsInit = false });
      }
    }
    else if (groupName.StartsWith("#TT"))
    {
      foreach (var tmAddrString in groupName.Split(';'))
      {
        if (!TmAddr.TryParse(tmAddrString, out var tmAddr, TmType.Analog))
        {
          throw new Exception($"Недопустимый адрес измерения в группе: {tmAddrString}");
        }
        tmAnalogs.Add(new TmAnalog(tmAddr));
      }
    }
    else
    {
      var groupAnalogs = _api.GetTagsByGroup(TmType.Analog, groupName)
                             .GetAwaiter()
                             .GetResult()
                             ?.Cast<TmAnalog>()
                             .ToList();
      if (groupAnalogs == null)
      {
        throw new Exception($"Ошибка загрузки данных группы: \"{groupName}\"");
      }
      tmAnalogs.AddRange(groupAnalogs);
    }
    _analogGroups.Add(groupIdx, new TmAnalogGroup(groupName, isUpdating, tmAnalogs));

    return tmAnalogs.Select((_, idx) => new[] { groupIdx, idx }).ToArray();
  }


  private int[][] InitTmAnalogGroupInput(int idx)
  {
    return InitTmAnalogGroup(idx, isUpdating: true);
  }


  private int[][] InitTmAnalogGroupOutput(int idx)
  {
    return InitTmAnalogGroup(idx, isUpdating: false);
  }


  private TmStatus FindTmStatus(int[] id)
  {
    if (id == null || id.Length != 2)
    {
      throw new Exception("Некорретный идентификатор сигнала");
    }
    if (!_statusGroups.TryGetValue(id[0], out var group))
    {
      throw new Exception("Несуществующий номер группы");
    }
    var tmStatus = group.Statuses.ElementAtOrDefault(id[1]);
    if (tmStatus == null)
    {
      throw new Exception("Несуществующий номер сигнала");
    }
    return tmStatus;
  }


  private TmAnalog FindTmAnalog(int[] id)
  {
    if (id == null || id.Length != 2)
    {
      throw new Exception("Некорретный идентификатор измерения");
    }
    if (!_analogGroups.TryGetValue(id[0], out var group))
    {
      throw new Exception("Несуществующий номер группы");
    }
    var tmAnalog = group.Analogs.ElementAtOrDefault(id[1]);
    if (tmAnalog == null)
    {
      throw new Exception("Несуществующий номер измерения");
    }
    return tmAnalog;
  }


  private bool IsTmStatusOn(int[] id)
  {
    return GetTmStatus(id) > 0;
  }


  private bool IsTmStatusOff(int[] id)
  {
    return GetTmStatus(id) == 0;
  }


  private int GetTmStatus(int[] id)
  {
    return FindTmStatus(id).Status;
  }


  private bool IsTmStatusFlagRaised(int[] id, TmFlags flag)
  {
    return FindTmStatus(id).HasFlag(flag);
  }


  private bool IsTmAnalogFlagRaised(int[] id, TmFlags flag)
  {
    return FindTmAnalog(id).HasFlag(flag);
  }


  private float GetTmAnalog(int[] id)
  {
    return FindTmAnalog(id).Value;
  }


  private float[] GetTmAnalogRetro(int[] id, long utcStartTime, long utcEndTime, int? step = null)
  {
    var tmAnalog = FindTmAnalog(id);

    if (!tmAnalog.IsInit)
    {
      LogDebug("Запрашивается ретроспектива для неинициализированного измерения");
      return Array.Empty<float>();
    }

    var retro = _api.GetAnalogRetro(tmAnalog, new TmAnalogRetroFilter(utcStartTime, utcEndTime, step))
                    .GetAwaiter()
                    .GetResult();

    if (retro == null)
    {
      throw new Exception("Ошибка получения ретроспективы измерения");
    }
    if (retro.Any(r => r.IsUnreliable))
    {
      _isErrorFlagRaised = true;
    }
    return retro.Select(r => r.Value).ToArray();
  }


  private float[] GetTmAnalogImpulseArchiveAverage(int[] id, long utcStartTime, long utcEndTime, int? step = null)
  {
    var tmAnalog = FindTmAnalog(id);

    if (!tmAnalog.IsInit)
    {
      LogDebug("Запрашивается импульс-архив для неинициализированного измерения");
      return Array.Empty<float>();
    }

    var retro = _api.GetImpulseArchiveAverage(tmAnalog, new TmAnalogRetroFilter(utcStartTime, utcEndTime, step))
                    .GetAwaiter()
                    .GetResult();

    if (retro == null)
    {
      throw new Exception("Ошибка получения ретроспективы измерения");
    }
    if (retro.Any(r => r.IsUnreliable))
    {
      _isErrorFlagRaised = true;
    }
    return retro.Select(r => r.Value).ToArray();
  }


  private void SetTmStatus(int[] id, int status)
  {
    var (ch, rtu, point) = FindTmStatus(id).TmAddr.GetTuple();
    _api.SetStatus(ch, rtu, point, status);
  }


  private void SetTmStatusOn(int[] id)
  {
    SetTmStatus(id, 1);
  }


  private void SetTmStatusOff(int[] id)
  {
    SetTmStatus(id, 0);
  }


  private void SetTmAnalog(int[] id, float value)
  {
    var (ch, rtu, point) = FindTmAnalog(id).TmAddr.GetTuple();
    _api.SetAnalog(ch, rtu, point, value);
  }


  private void RaiseTmStatusFlags(int[] id, TmFlags flags)
  {
    var (ch, rtu, point) = FindTmStatus(id).TmAddr.GetTuple();
    _api.SetTagFlagsExplicitly(new TmStatus(ch, rtu, point), flags);
  }


  private void ClearTmStatusFlags(int[] id, TmFlags flags)
  {
    var (ch, rtu, point) = FindTmStatus(id).TmAddr.GetTuple();
    _api.ClearTagFlagsExplicitly(new TmStatus(ch, rtu, point), flags);
  }


  private void RaiseTmAnalogFlags(int[] id, TmFlags flags)
  {
    var (ch, rtu, point) = FindTmAnalog(id).TmAddr.GetTuple();
    _api.SetTagFlagsExplicitly(new TmAnalog(ch, rtu, point), flags);
  }


  private void ClearTmAnalogFlags(int[] id, TmFlags flags)
  {
    var (ch, rtu, point) = FindTmAnalog(id).TmAddr.GetTuple();
    _api.ClearTagFlagsExplicitly(new TmAnalog(ch, rtu, point), flags);
  }


  private int GetPeriod()
  {
    return _period;
  }


  private void OverridePeriod(int period)
  {
    _period = period;
  }


  private void ClearErrorFlag()
  {
    _isErrorFlagRaised = false;
  }


  private bool IsErrorFlagRaised()
  {
    return _isErrorFlagRaised;
  }


  private void ThrowException(string message)
  {
    throw new Exception(message);
  }


  private void LogError(string message)
  {
    Tms.PrintError($"Ошибка скрипта \"{_name}\": {message}");
  }


  private void LogDebug(string message)
  {
    Tms.PrintDebug($"Отладочное сообщение скрипта \"{_name}\": {message}");
  }


  private class TmStatusGroup
  {
    public string         Name       { get; }
    public bool           IsUpdating { get; }
    public List<TmStatus> Statuses   { get; }


    public TmStatusGroup(string name, bool isUpdating, List<TmStatus> statuses)
    {
      Name       = name;
      IsUpdating = isUpdating;
      Statuses   = new List<TmStatus>(statuses ?? new List<TmStatus>());
    }
  }


  private class TmAnalogGroup
  {
    public string         Name       { get; }
    public bool           IsUpdating { get; }
    public List<TmAnalog> Analogs    { get; }


    public TmAnalogGroup(string name, bool isUpdating, List<TmAnalog> analogs)
    {
      Name       = name;
      IsUpdating = isUpdating;
      Analogs    = new List<TmAnalog>(analogs ?? new List<TmAnalog>());
    }
  }
}