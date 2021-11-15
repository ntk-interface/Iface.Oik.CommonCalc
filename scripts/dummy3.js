const tsSwitch = InitTmStatusGroupOutput(0);

const tsTrolley = InitTmStatusGroupInput(1);

if (tsSwitch.length !== tsTrolley.length)
{
  ThrowException("Группы коммутационных аппаратов и тележек должны совпадать по количеству");
}

const length = tsSwitch.length;

function DoWork()
{
  for (let i = 0; i < length; i++)
  {
    if (IsTmStatusOn(tsTrolley[i]))
    {
      RaiseTmStatusFlags(tsSwitch[i], TmFlagLevelA);
    }
    else
    {
      ClearTmStatusFlags(tsSwitch[i], TmFlagLevelA);
    }
  }
}