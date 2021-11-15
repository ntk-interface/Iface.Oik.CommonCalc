const analogs = InitTmAnalogGroupInput(0);

function DoWork()
{
  for (let analog of analogs)
  {
    const endTime = new Date();
    endTime.setMinutes(0, 0, 0);
    
    const utcEndTime = Math.floor(endTime.getTime() / 1000) - endTime.getTimezoneOffset() * 60;
    
    const utcStartTime = utcEndTime - 60 * 60 * 10;
    
    const retro = GetTmAnalogImpulseArchiveAverage(analog, utcStartTime, utcEndTime, 60 * 60);
    
    for (let r of retro)
    {
      LogDebug(r);
    }
  }
}