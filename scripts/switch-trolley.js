const outputSwitches = InitTmStatusGroupOutput(0);

const inputTrolleyMaintenance = InitTmStatusGroupInput(1);
const inputTrolleyCheckup = TryInitTmStatusGroupInput(2);

if (inputTrolleyMaintenance.length !== outputSwitches.length)
{
  ThrowException("Группы коммутационных аппаратов и тележек должны совпадать по количеству");
}
if (inputTrolleyCheckup && inputTrolleyCheckup.length !== outputSwitches.length)
{
  ThrowException("Группы коммутационных аппаратов и тележек должны совпадать по количеству");
}

function DoWork()
{
  for (let i = 0; i < outputSwitches.length; i++)
  {
    if (IsTmStatusOn(inputTrolleyMaintenance[i]))
    {
      RaiseTmStatusFlags(outputSwitches[i], TmFlagLevelA);
    }
    else
    {
      ClearTmStatusFlags(outputSwitches[i], TmFlagLevelA);
    }
    if (inputTrolleyCheckup)
    {
      if (IsTmStatusOn(inputTrolleyCheckup[i]))
      {
        RaiseTmStatusFlags(outputSwitches[i], TmFlagLevelB);
      }
      else
      {
        ClearTmStatusFlags(outputSwitches[i], TmFlagLevelB);
      }
    }
  }
}