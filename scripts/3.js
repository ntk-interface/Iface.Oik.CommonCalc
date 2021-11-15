const tsSwitch = GetGroupArray(4);

const tsTrolley = GetGroupArray(5);

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