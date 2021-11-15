const tsOn = GetGroupArray(2);

const tsOff = GetGroupArray(3);

function DoWork()
{
  for (let ts of tsOn)
  {
    SetTmStatus(ts, 1);
  }
  for (let ts of tsOff)
  {
    SetTmStatus(ts, 0);
  }
}