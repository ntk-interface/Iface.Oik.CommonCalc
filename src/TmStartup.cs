using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Iface.Oik.Tm.Helpers;
using Iface.Oik.Tm.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Iface.Oik.CommonCalc;

public class ServerService : CommonServerService, IHostedService
{
}


public class TmStartup : BackgroundService
{
  private const string ApplicationName = "CommonCalc";
  private const string TraceName       = "CommonCalc";
  private const string TraceComment    = "<Iface.Oik.CommonCalc>";

  private static int              _tmCid;
  private static TmUserInfo       _userInfo;
  private static TmServerFeatures _serverFeatures;
  private static IntPtr           _stopEventHandle;

  private readonly IHostApplicationLifetime _applicationLifetime;
  private readonly ICommonInfrastructure    _infr;


  public TmStartup(ICommonInfrastructure infr, IHostApplicationLifetime applicationLifetime)
  {
    _infr                = infr;
    _applicationLifetime = applicationLifetime;
  }


  public static void Connect()
  {
    var commandLineArgs = Environment.GetCommandLineArgs();

    (_tmCid, _userInfo, _serverFeatures, _stopEventHandle) = Tms.InitializeAsTaskWithoutSql(
      new TmOikTaskOptions
      {
        TraceName    = TraceName,
        TraceComment = TraceComment,
      },
      new TmInitializeOptions
      {
        ApplicationName = ApplicationName,
        TmServer        = commandLineArgs.ElementAtOrDefault(1) ?? "TMS",
        Host            = commandLineArgs.ElementAtOrDefault(2) ?? ".",
        User            = commandLineArgs.ElementAtOrDefault(3) ?? "",
        Password        = commandLineArgs.ElementAtOrDefault(4) ?? "",
      });

    Tms.PrintMessage("Соединение с сервером установлено");
  }


  public override Task StartAsync(CancellationToken cancellationToken)
  {
    _infr.InitializeTmWithoutSql(_tmCid, _userInfo, _serverFeatures);

    return base.StartAsync(cancellationToken);
  }


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      if (await Task.Run(() => Tms.StopEventSignalDuringWait(_stopEventHandle, 1000), stoppingToken))
      {
        Tms.PrintMessage("Получено сообщение об остановке со стороны сервера");
        _applicationLifetime.StopApplication();
        break;
      }
    }
  }


  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    _infr.TerminateTm();

    Tms.TerminateWithoutSql(_tmCid);

    Tms.PrintMessage("Задача будет закрыта");

    await base.StopAsync(cancellationToken);
  }
}